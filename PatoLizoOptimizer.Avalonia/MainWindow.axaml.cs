using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public partial class MainWindow : Window
{
    private bool _busy;
    private UpdateManifest? _availableUpdate;
    public MainWindow()
    {
        InitializeComponent();
        UpdateState();
        Opened += async (_, _) =>
        {
            if (await EnsureLicense())
            {
                await EnsureTerms();
                _availableUpdate = await UpdateService.CheckAsync();
                UpdateButton.IsVisible = _availableUpdate is not null;
            }
        };
    }

    private async void Update_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy || _availableUpdate is null) return;
        SetBusy(true);
        try
        {
            StateTitle.Text = $"Baixando Ducktimizer {_availableUpdate.Version}…";
            var progress = new Progress<int>(percent => { ApplyProgress.Value = percent; StateDetail.Text = $"{percent}% baixado — verificando integridade"; });
            var installer = await UpdateService.DownloadAsync(_availableUpdate, progress);
            UpdateService.LaunchInstaller(installer);
            Close();
        }
        catch (Exception ex)
        {
            AppLogger.Error(ex, "Falha ao atualizar");
            SetBusy(false);
            StateTitle.Text = "Atualização não concluída";
            StateDetail.Text = ex.Message;
        }
    }

    private void Preset_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (PresetBox is null) return;
        var level = PresetBox.SelectedIndex;
        var debloatOnly = level == 4;
        VisualSwitch.IsChecked = !debloatOnly;
        GamingSwitch.IsChecked = !debloatOnly && level >= 1;
        PowerSwitch.IsChecked = !debloatOnly && level >= 1;
        PrivacySwitch.IsChecked = level >= 2;
        DrySwitch.IsChecked = !debloatOnly && level >= 3;
        SearchSwitch.IsChecked = level >= 2;
        DebloatSwitch.IsChecked = debloatOnly || level == 3;
        BackgroundSwitch.IsChecked = debloatOnly || level == 3;
    }

    private async void Activate_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var groups = SelectedGroups();
        if (groups.Count == 0) return;
        var warning = groups.Contains("debloat")
            ? "\n\nDEBLOAT EXTREMO: apps listados serão desinstalados para todos os usuários. A restauração automática desses apps depende dos arquivos ainda existirem no Windows."
            : "";
        if (!await Confirm("Ativar otimizações", $"Aplicar {groups.Count} grupos selecionados?\n\nO app tentará criar um ponto de restauração e salvará o estado das configurações.{warning}")) return;
        SetBusy(true);
        try { if (await RunOperation("--apply", string.Join(',', groups))) UpdateState(); }
        finally { SetBusy(false); }
    }

    private async void Deactivate_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!OptimizerEngine.HasActiveBackups()) return;
        if (!await Confirm("Desativar otimizações", "Restaurar todas as execuções do Ducktimizer e voltar ao estado anterior?")) return;
        SetBusy(true);
        try
        {
            if (await RunOperation("--undo-all"))
            {
                VisualSwitch.IsChecked = GamingSwitch.IsChecked = PowerSwitch.IsChecked = false;
                PrivacySwitch.IsChecked = DrySwitch.IsChecked = SearchSwitch.IsChecked = false;
                DebloatSwitch.IsChecked = BackgroundSwitch.IsChecked = false;
                UpdateState();
            }
        }
        finally { SetBusy(false); }
    }

    private List<string> SelectedGroups()
    {
        var result = new List<string>();
        if (VisualSwitch.IsChecked == true) result.Add("visual");
        if (GamingSwitch.IsChecked == true) result.Add("gaming");
        if (PowerSwitch.IsChecked == true) result.Add("power");
        if (PrivacySwitch.IsChecked == true) result.Add("privacy");
        if (DrySwitch.IsChecked == true) result.Add("dry");
        if (SearchSwitch.IsChecked == true) result.Add("search");
        if (DebloatSwitch.IsChecked == true) result.Add("debloat");
        if (BackgroundSwitch.IsChecked == true) result.Add("background");
        return result;
    }

    private async Task<bool> RunOperation(params string[] args)
    {
        var progress = new Progress<OperationProgress>(value =>
        {
            ApplyProgress.Value = value.Percent;
            StateTitle.Text = value.Message;
            StateDetail.Text = $"{value.Percent}% concluído — não feche o aplicativo";
        });
        var ok = await Task.Run(() => OptimizerEngine.RunElevated(args, progress) == 0);
        if (!ok && !string.IsNullOrWhiteSpace(OptimizerEngine.LastError))
        {
            StateTitle.Text = "Não foi possível concluir";
            StateDetail.Text = OptimizerEngine.LastError;
        }
        return ok;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ActivateButton.IsEnabled = DeactivateButton.IsEnabled = UpdateButton.IsEnabled = !busy;
        ApplyProgress.IsVisible = busy;
        if (busy) ApplyProgress.Value = 0;
        if (busy)
        {
            StateTitle.Text = "Aplicando alterações…";
            StateDetail.Text = "Não feche o Ducktimizer durante esta etapa";
        }
        else UpdateState();
    }

    private void UpdateState()
    {
        var active = OptimizerEngine.HasActiveBackups();
        DeactivateButton.IsEnabled = active && !_busy;
        StateTitle.Text = active ? "Otimizações ativas" : "Sistema no estado base";
        StateDetail.Text = active ? "Use Desativar para restaurar todas as execuções" : "Selecione os grupos e aplique quando quiser";
    }

    private async Task<bool> Confirm(string title, string message)
    {
        var result = false;
        var dialog = new Window { Title = title, Width = 430, Height = 230, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Avalonia.Media.Brush.Parse("#202429") };
        var yes = new Button { Content = "Confirmar", Classes = { "primary" }, MinWidth = 110 };
        var no = new Button { Content = "Cancelar", MinWidth = 100 };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(24), Spacing = 18, Children = {
            new TextBlock { Text = title, FontSize = 20, FontWeight = Avalonia.Media.FontWeight.Bold },
            new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brush.Parse("#B7C4CA") },
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { no, yes } }
        }};
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<bool> EnsureLicense()
    {
        if (LicenseService.IsActivated(out var installed, out _) && installed is not null)
        {
            var licenseStatus = await LicenseService.CheckStatusAsync(installed);
            if (licenseStatus.Success) return true;
        }
        var activated = false;
        var dialog = new Window { Title = "Ativar Ducktimizer", Width = 560, Height = 410, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Avalonia.Media.Brush.Parse("#202429") };
        var device = new TextBox { Text = LicenseService.DeviceId, IsReadOnly = true };
        var token = new TextBox { PlaceholderText = "Cole aqui a licença enviada pelo vendedor", AcceptsReturn = true, Height = 90, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var status = new TextBlock { Foreground = Avalonia.Media.Brush.Parse("#FF9C9C"), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var copy = new Button { Content = "Copiar ID" };
        var activate = new Button { Content = "Ativar licença", Classes = { "primary" } };
        var exit = new Button { Content = "Sair" };
        copy.Click += (_, _) => { device.SelectAll(); device.Copy(); };
        activate.Click += async (_, _) =>
        {
            activate.IsEnabled = false;
            status.Text = "Ativando…";
            var result = await LicenseService.ActivateAsync(token.Text ?? "");
            if (result.Success)
            {
                activated = true;
                dialog.Close();
            }
            else
            {
                status.Text = result.Reason;
                activate.IsEnabled = true;
            }
        };
        exit.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Avalonia.Thickness(26), Spacing = 12, Children = {
            new TextBlock { Text = "Ative sua cópia", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold },
            new TextBlock { Text = "Cole a chave recebida. Na primeira ativação ela será vinculada automaticamente a este computador e não poderá ativar outro.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brush.Parse("#BAC7CD") },
            new TextBlock { Text = "ID deste dispositivo", FontWeight = Avalonia.Media.FontWeight.SemiBold },
            new Grid { ColumnDefinitions = ColumnDefinitions.Parse("*,Auto"), ColumnSpacing = 8, Children = { device, copy } },
            token, status,
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { exit, activate } }
        }};
        Grid.SetColumn(copy, 1);
        await dialog.ShowDialog(this);
        if (!activated) Close();
        return activated;
    }

    private async Task EnsureTerms()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ducktimizer");
        var acceptedFile = Path.Combine(folder, "terms-v3.4.2.accepted");
        if (File.Exists(acceptedFile)) return;
        var accepted = false;
        var dialog = new Window { Title = "Termos de uso", Width = 620, Height = 470, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Avalonia.Media.Brush.Parse("#202429") };
        var check = new CheckBox { Content = "Li e aceito os termos e o aviso de risco." };
        var accept = new Button { Content = "Aceitar e continuar", Classes = { "primary" }, IsEnabled = false };
        var exit = new Button { Content = "Sair" };
        check.IsCheckedChanged += (_, _) => accept.IsEnabled = check.IsChecked == true;
        accept.Click += (_, _) => { accepted = true; dialog.Close(); };
        exit.Click += (_, _) => dialog.Close();
        var heading = new TextBlock { Text = "Antes de otimizar", FontSize = 24, FontWeight = Avalonia.Media.FontWeight.Bold };
        var termsText = "TERMOS DE USO E CIÊNCIA DE RISCO — v3.4.2\n\n" +
            "1. FINALIDADE. O Ducktimizer modifica configurações do Windows com o objetivo de reduzir recursos em segundo plano, efeitos visuais, telemetria e aplicativos pré-instalados. Não existe garantia de aumento de FPS, redução de latência ou melhoria específica.\n\n" +
            "2. ADMINISTRADOR. O programa exige privilégios de administrador desde a abertura. Ao aceitar o aviso do Windows, você autoriza alterações no Registro, políticas locais, plano de energia e pacotes de aplicativos deste computador.\n\n" +
            "3. MODOS AGRESSIVOS. Windows seco desativa suavização de fontes, sombras, transparência e efeitos para priorizar uma interface mínima. Pesquisa enxuta remove Bing, destaques e sugestões. Debloat Extremo desinstala aplicativos empacotados e remove seu provisionamento. Edge, Microsoft Store, Windows Update, Defender, Firewall e serviços críticos não são removidos.\n\n" +
            "4. BACKUP E RESTAURAÇÃO. O programa tenta criar um ponto de restauração e registra o estado anterior das configurações. A restauração de Registro e energia é automatizada. A reinstalação de aplicativos removidos não é garantida, pois depende de os arquivos ou pacotes continuarem disponíveis no Windows ou na Microsoft Store.\n\n" +
            "5. EFEITOS POSSÍVEIS. As mudanças podem alterar aparência, notificações, pesquisa, recursos de IA, sincronização, consumo de energia, compatibilidade de aplicativos e comportamento após atualizações do Windows. Algumas alterações exigem reinicialização.\n\n" +
            "6. RESPONSABILIDADE DO USUÁRIO. Use somente em computadores próprios ou autorizados, salve arquivos importantes e revise os grupos selecionados antes de confirmar. O usuário assume a responsabilidade pela escolha dos presets e pelas consequências das mudanças.\n\n" +
            "7. PRIVACIDADE. O aplicativo não envia arquivos pessoais ou telemetria comportamental. Ele consulta o servidor para ativação, validação periódica da licença e atualização, transmitindo versão, identificador da licença e Device ID quando necessário. Logs e backups ficam armazenados localmente.\n\n" +
            "8. LICENÇA. A licença é individual e vinculada automaticamente ao primeiro dispositivo em que for ativada. O app renova uma autorização assinada de até 7 dias para permitir uso temporário sem conexão. Não é permitido compartilhar, revender, alterar ou contornar o mecanismo de ativação. Trocas relevantes de hardware podem exigir uma nova ativação fornecida pelo vendedor.\n\n" +
            "Este texto é um modelo técnico e deve passar por revisão jurídica antes da comercialização.";
        var terms = new ScrollViewer { Margin = new Avalonia.Thickness(0,16), Content = new TextBlock { Text = termsText, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Avalonia.Media.Brush.Parse("#BAC7CD") } };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Avalonia.Thickness(0,16,0,0), Children = { exit, accept } };
        Grid.SetRow(terms, 1); Grid.SetRow(check, 2); Grid.SetRow(buttons, 3);
        dialog.Content = new Grid { Margin = new Avalonia.Thickness(26), RowDefinitions = RowDefinitions.Parse("Auto,*,Auto,Auto"), Children = { heading, terms, check, buttons } };
        await dialog.ShowDialog(this);
        if (!accepted) { Close(); return; }
        Directory.CreateDirectory(folder);
        File.WriteAllText(acceptedFile, "accepted-v3.4.2");
    }
}
