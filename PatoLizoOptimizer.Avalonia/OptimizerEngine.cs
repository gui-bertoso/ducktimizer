using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public record RegChange(string Hive, string Path, string Name, string Value, RegistryValueKind Kind);
public record RegBackup(string Hive, string Path, string Name, bool Exists, string? Value, RegistryValueKind Kind);
public record OperationProgress(int Percent, string Message);
public sealed class RunBackup
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string[] Groups { get; set; } = [];
    public List<RegBackup> Registry { get; set; } = [];
    public string? PowerScheme { get; set; }
    public string RestorePoint { get; set; } = "not-attempted";
    public List<string> RemovedApps { get; set; } = [];
    public bool Complete { get; set; }
}

public static class OptimizerEngine
{
    private static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ducktimizer");
    private static readonly string LegacyRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PatoLizoOptimizer");
    private static readonly string Backups = Path.Combine(Root, "backups");
    private const uint YesNoWarning = 0x34;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBoxW(IntPtr h, string text, string title, uint type);

    public static string? LastError { get; private set; }

    public static int RunElevated(string[] args, IProgress<OperationProgress>? progress = null)
    {
        try
        {
            LastError = null;
            if (args.Length == 0) return 2;
            if (!IsAdministrator())
                throw new UnauthorizedAccessException("O processo não recebeu permissão de administrador. Feche o app, abra novamente e aceite o aviso do UAC.");
            if (args[0] == "--apply" && !LicenseService.IsActivated(out _, out var licenseReason))
                throw new UnauthorizedAccessException($"Ativação necessária: {licenseReason}");
            AppLogger.Info($"Operação solicitada: {args[0]}");
            if (args[0] == "--apply") Apply(args.Length > 1 ? args[1].Split(',', StringSplitOptions.RemoveEmptyEntries) : [], progress);
            else if (args[0] == "--undo-all") UndoAll(progress);
            else return 2;
            MessageBoxW(IntPtr.Zero, "Operação concluída. Reinicie o Windows para consolidar todas as mudanças.", "Ducktimizer", 0x40);
            AppLogger.Info("Operação concluída com sucesso");
            return 0;
        }
        catch (OperationCanceledException) { return 3; }
        catch (Exception ex)
        {
            LastError = FriendlyError(ex);
            AppLogger.Error(ex, "Falha na operação administrativa");
            MessageBoxW(IntPtr.Zero, LastError, "Ducktimizer — erro", 0x10);
            return 1;
        }
    }

    private static string FriendlyError(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "O Windows negou uma alteração. Confirme que o UAC foi aceito e que nenhuma política de segurança bloqueia o Ducktimizer.",
        TimeoutException => "Uma etapa demorou mais que o limite de segurança e foi interrompida. Reinicie o computador e tente novamente com menos grupos.",
        IOException => "Não foi possível salvar ou ler o backup local. Verifique espaço em disco e permissões da pasta do Ducktimizer.",
        JsonException => "Um backup local está corrompido. Consulte o log antes de aplicar novas alterações.",
        OperationCanceledException => "Operação cancelada. Nenhuma nova etapa foi iniciada.",
        _ => $"A operação não pôde ser concluída. Detalhe: {ex.Message}\n\nO backup das etapas iniciadas foi preservado; use Restaurar se necessário."
    };

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void Apply(string[] groups, IProgress<OperationProgress>? progress = null)
    {
        progress?.Report(new(2, "Preparando backup local…"));
        MigrateLegacyBackups();
        Directory.CreateDirectory(Backups);
        var backup = new RunBackup { Groups = groups };
        progress?.Report(new(6, "Criando ponto de restauração…"));
        if (TryRestorePoint(out var reason)) backup.RestorePoint = "created";
        else
        {
            backup.RestorePoint = "unavailable: " + reason;
            var answer = MessageBoxW(IntPtr.Zero,
                "O ponto de restauração do Windows não pôde ser criado. Nenhuma alteração foi feita.\n\nContinuar somente com o backup granular reversível do app?",
                "Restauração indisponível", YesNoWarning);
            if (answer != 6) throw new OperationCanceledException();
        }

        var path = Path.Combine(Backups, $"backup-{DateTime.Now:yyyyMMdd-HHmmss-ffff}.json");
        try
        {
            var changes = Changes(groups).DistinctBy(x => (x.Hive, x.Path, x.Name)).ToList();
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index];
                progress?.Report(new(10 + (int)(60d * index / Math.Max(1, changes.Count)), $"Aplicando configuração {index + 1} de {changes.Count}…"));
                backup.Registry.Add(Read(change));
                Write(change);
            }
            if (groups.Contains("power"))
            {
                progress?.Report(new(74, "Ativando plano de alto desempenho…"));
                backup.PowerScheme = ActivePowerScheme();
                Run("powercfg.exe", "/setactive SCHEME_MIN", true);
            }
            if (groups.Contains("debloat"))
            {
                progress?.Report(new(82, "Removendo aplicativos selecionados…"));
                backup.RemovedApps = RemoveBloatware();
            }
            progress?.Report(new(96, "Gravando estado de restauração…"));
            backup.Complete = true;
        }
        finally { File.WriteAllText(path, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true })); }
        progress?.Report(new(100, "Otimizações aplicadas."));
    }

    public static void UndoAll(IProgress<OperationProgress>? progress = null)
    {
        progress?.Report(new(3, "Localizando backups…"));
        MigrateLegacyBackups();
        if (!Directory.Exists(Backups)) return;
        var paths = Directory.GetFiles(Backups, "backup-*.json").Where(p => !p.Contains(".restored.")).OrderByDescending(p => p).ToList();
        for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
        {
            var path = paths[pathIndex];
            progress?.Report(new(8 + (int)(84d * pathIndex / Math.Max(1, paths.Count)), $"Restaurando backup {pathIndex + 1} de {paths.Count}…"));
            var backup = JsonSerializer.Deserialize<RunBackup>(File.ReadAllText(path)) ?? throw new InvalidDataException("Backup inválido.");
            if (backup.PowerScheme is not null) Run("powercfg.exe", $"/setactive {backup.PowerScheme}", true);
            foreach (var item in backup.Registry.AsEnumerable().Reverse()) Restore(item);
            if (backup.RemovedApps?.Count > 0) RestoreApps(backup.RemovedApps);
            File.Move(path, Path.ChangeExtension(path, ".restored.json"), true);
        }
        progress?.Report(new(100, "Sistema restaurado."));
    }

    public static bool HasActiveBackups()
    {
        MigrateLegacyBackups();
        return Directory.Exists(Backups) && Directory.GetFiles(Backups, "backup-*.json").Any(p => !p.Contains(".restored."));
    }

    private static void MigrateLegacyBackups()
    {
        var legacy = Path.Combine(LegacyRoot, "backups");
        if (!Directory.Exists(legacy)) return;
        Directory.CreateDirectory(Backups);
        foreach (var file in Directory.GetFiles(legacy, "backup-*.json"))
        {
            var target = Path.Combine(Backups, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
    }

    public static bool SelfTest()
    {
        var groups = new[] { "visual", "gaming", "power", "privacy", "dry", "search", "debloat", "background" };
        var changes = Changes(groups).ToList();
        var sample = new RunBackup { Groups = groups, Registry = [new RegBackup("HKCU", "Test", "Value", false, null, RegistryValueKind.DWord)] };
        var roundTrip = JsonSerializer.Deserialize<RunBackup>(JsonSerializer.Serialize(sample));
        return changes.Count >= 70 && BloatwarePackages.Length >= 35 && changes.Select(x => (x.Hive, x.Path, x.Name)).Distinct().Count() == changes.Count
            && roundTrip?.Groups.Length == groups.Length;
    }

    private static IEnumerable<RegChange> Changes(IEnumerable<string> selected)
    {
        var groups = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (groups.Contains("visual"))
        {
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
            yield return S("HKCU", @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
            yield return S("HKCU", @"Control Panel\Desktop", "MenuShowDelay", "0");
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 0);
            yield return S("HKCU", @"Control Panel\Desktop", "DragFullWindows", "0");
        }
        if (groups.Contains("gaming"))
        {
            yield return D("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
            yield return D("HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AudioCaptureEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "CursorCaptureEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
        }
        if (groups.Contains("privacy"))
        {
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0);
            yield return D("HKCU", @"Software\Microsoft\Input\TIPC", "Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1);
            yield return D("HKCU", @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1);
            yield return D("HKCU", @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0);
            yield return D("HKCU", @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0);
            yield return D("HKLM", @"Software\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "DiagnosticData", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "ShowedToastAtLevel", 1);
        }
        if (groups.Contains("dry"))
        {
            yield return S("HKCU", @"Control Panel\Desktop", "FontSmoothing", "0");
            yield return S("HKCU", @"Control Panel\Desktop", "FontSmoothingType", "1");
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisablePreviewDesktop", 1);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\AnimateMinMax", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\ComboBoxAnimation", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\CursorShadow", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\DropShadow", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\ListBoxSmoothScrolling", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\MenuAnimation", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\SelectionFade", "DefaultApplied", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\TooltipAnimation", "DefaultApplied", 0);
        }
        if (groups.Contains("search"))
        {
            yield return D("HKCU", @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDeviceSearchHistoryEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "SafeSearchMode", 0);
        }
        if (groups.Contains("debloat"))
        {
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
            yield return D("HKCU", @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
            yield return D("HKCU", @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots", 1);
            yield return D("HKCU", @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "StartupBoostEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "ComposeInlineEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "NewTabPageContentEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Edge", "ShowRecommendationsEnabled", 0);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Windows\CloudContent", "DisableCloudOptimizedContent", 1);
            yield return D("HKLM", @"Software\Policies\Microsoft\Dsh", "AllowWidgets", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0);
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0);
        }
        if (groups.Contains("background"))
            yield return D("HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
    }

    private static RegChange D(string h, string p, string n, int v) => new(h, p, n, v.ToString(), RegistryValueKind.DWord);
    private static RegChange S(string h, string p, string n, string v) => new(h, p, n, v, RegistryValueKind.String);
    private static RegistryKey Base(string hive) => hive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
    private static RegBackup Read(RegChange c)
    {
        using var key = Base(c.Hive).OpenSubKey(c.Path);
        var value = key?.GetValue(c.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return new(c.Hive, c.Path, c.Name, false, null, c.Kind);
        return new(c.Hive, c.Path, c.Name, true, Convert.ToString(value), key!.GetValueKind(c.Name));
    }
    private static void Write(RegChange c)
    {
        try
        {
            using var key = Base(c.Hive).CreateSubKey(c.Path, true);
            object value = c.Kind == RegistryValueKind.DWord ? int.Parse(c.Value) : c.Value;
            key.SetValue(c.Name, value, c.Kind);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Acesso negado ao Registro: {c.Hive}\\{c.Path} → {c.Name}. Verifique o UAC ou políticas de segurança do PC.", ex);
        }
    }
    private static void Restore(RegBackup b)
    {
        try
        {
            using var key = Base(b.Hive).CreateSubKey(b.Path, true);
            if (!b.Exists) { try { key.DeleteValue(b.Name, false); } catch { } return; }
            object value = b.Kind == RegistryValueKind.DWord ? int.Parse(b.Value ?? "0") : b.Value ?? "";
            key.SetValue(b.Name, value, b.Kind);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Acesso negado ao restaurar: {b.Hive}\\{b.Path} → {b.Name}.", ex);
        }
    }
    private static string ActivePowerScheme()
    {
        var text = Run("powercfg.exe", "/getactivescheme", true);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"[0-9a-fA-F-]{36}");
        if (!match.Success) throw new InvalidOperationException("Não foi possível ler o plano de energia.");
        return match.Value;
    }

    private static readonly string[] BloatwarePackages =
    [
        "Clipchamp.Clipchamp", "Microsoft.3DBuilder", "Microsoft.549981C3F5F10",
        "Microsoft.BingFinance", "Microsoft.BingFoodAndDrink", "Microsoft.BingHealthAndFitness",
        "Microsoft.BingNews", "Microsoft.BingSports", "Microsoft.BingTranslator", "Microsoft.BingTravel",
        "Microsoft.BingWeather", "Microsoft.Windows.AIHub", "Microsoft.Copilot", "Microsoft.GetHelp", "Microsoft.Getstarted", "Microsoft.Messaging",
        "Microsoft.Microsoft3DViewer", "Microsoft.MicrosoftJournal", "Microsoft.MicrosoftOfficeHub",
        "Microsoft.MicrosoftPowerBIForWindows", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.MixedReality.Portal",
        "Microsoft.NetworkSpeedTest", "Microsoft.News", "Microsoft.Office.Sway", "Microsoft.OneConnect",
        "Microsoft.Print3D", "Microsoft.PowerAutomateDesktop", "Microsoft.SkypeApp", "Microsoft.Todos",
        "Microsoft.Windows.DevHome", "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps", "Microsoft.XboxApp",
        "Microsoft.ZuneMusic", "Microsoft.ZuneVideo", "MicrosoftCorporationII.MicrosoftFamily", "MicrosoftTeams", "MSTeams",
        "king.com.CandyCrushSaga", "king.com.CandyCrushSodaSaga", "SpotifyAB.SpotifyMusic",
        "BytedancePte.Ltd.TikTok", "Facebook.Instagram"
    ];

    private static List<string> RemoveBloatware()
    {
        var list = string.Join(',', BloatwarePackages.Select(x => "'" + x.Replace("'", "''") + "'"));
        var script = "$ids=@(" + list + ");$removed=@();foreach($id in $ids){" +
            "$pkgs=Get-AppxPackage -AllUsers -Name $id -ErrorAction SilentlyContinue;" +
            "foreach($p in $pkgs){Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction SilentlyContinue;if(-not(Get-AppxPackage -AllUsers -Name $p.Name -ErrorAction SilentlyContinue)){$removed+=$p.Name}};" +
            "Get-AppxProvisionedPackage -Online|Where-Object DisplayName -eq $id|Remove-AppxProvisionedPackage -Online -AllUsers -ErrorAction SilentlyContinue|Out-Null};" +
            "$removed|Sort-Object -Unique";
        return RunPowerShell(script).Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
    }

    private static void RestoreApps(IEnumerable<string> names)
    {
        var list = string.Join(',', names.Select(x => "'" + x.Replace("'", "''") + "'"));
        var script = "$names=@(" + list + ");foreach($n in $names){" +
            "$m=Get-AppxPackage -AllUsers -Name $n -ErrorAction SilentlyContinue|Select-Object -First 1;" +
            "if($m -and (Test-Path ($m.InstallLocation+'\\AppxManifest.xml'))){Add-AppxPackage -DisableDevelopmentMode -Register ($m.InstallLocation+'\\AppxManifest.xml') -ErrorAction SilentlyContinue}}";
        RunPowerShell(script);
    }

    private static string RunPowerShell(string script)
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ErrorActionPreference='Continue';" + script));
        return Run("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}", false);
    }
    private static bool TryRestorePoint(out string reason)
    {
        var script = "$ErrorActionPreference='Stop'; Enable-ComputerRestore -Drive ($env:SystemDrive+'\\'); Checkpoint-Computer -Description 'Ducktimizer' -RestorePointType MODIFY_SETTINGS";
        try { Run("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"", true); reason = ""; return true; }
        catch (Exception ex) { reason = ex.Message; return false; }
    }
    private static string Run(string file, string args, bool check)
    {
        using var process = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true })!;
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"A operação {Path.GetFileName(file)} excedeu 2 minutos.");
        }
        Task.WaitAll(outputTask, errorTask);
        var output = outputTask.Result; var error = errorTask.Result;
        if (check && process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? $"Falha ao executar {file}." : error.Trim());
        if (!string.IsNullOrWhiteSpace(error)) AppLogger.Info($"{Path.GetFileName(file)} retornou aviso: {error.Trim()}");
        return output;
    }
}
