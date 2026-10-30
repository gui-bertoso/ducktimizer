using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

const string appName = "Ducktimizer";
var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ducktimizer");
var appPath = Path.Combine(installDir, "Ducktimizer.exe");
var setupPath = Path.Combine(installDir, "Ducktimizer-Setup.exe");
try
{
    if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase)) { Uninstall(); return; }
    if (args.Contains("--create-shortcuts", StringComparer.OrdinalIgnoreCase)) { CreateShortcuts(); return; }
    Directory.CreateDirectory(installDir);
    using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ducktimizer.Payload.exe") ?? throw new InvalidDataException("Payload ausente.");
    using (var output = new FileStream(appPath, FileMode.Create, FileAccess.Write, FileShare.None)) payload.CopyTo(output);
    File.Copy(Environment.ProcessPath!, setupPath, true);
    var notice = Assembly.GetExecutingAssembly().GetManifestResourceStream("DEPENDENCY-LICENSE");
    if (notice is not null) using (var output = File.Create(Path.Combine(installDir, "DEPENDENCY-LICENSE"))) notice.CopyTo(output);
    using var license = Assembly.GetExecutingAssembly().GetManifestResourceStream("LICENSE");
    if (license is not null) using (var output = File.Create(Path.Combine(installDir, "LICENSE"))) license.CopyTo(output);
    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Ducktimizer");
    key?.SetValue("DisplayName", appName); key?.SetValue("DisplayVersion", "4.0.0"); key?.SetValue("InstallLocation", installDir); key?.SetValue("UninstallString", $"\"{setupPath}\" --uninstall");
    CreateShortcuts();
    Process.Start(new ProcessStartInfo(appPath) { UseShellExecute = true });
}
catch (Exception ex) { MessageBox(IntPtr.Zero, ex.Message, appName + " — erro", 0x10); Environment.ExitCode = 1; }

void Uninstall()
{
    try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Ducktimizer.lnk")); File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Ducktimizer.lnk")); Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Ducktimizer", false); File.Delete(appPath); File.Delete(Path.Combine(installDir, "DEPENDENCY-LICENSE")); File.Delete(Path.Combine(installDir, "LICENSE")); File.Delete(setupPath); Directory.Delete(installDir, false); MessageBox(IntPtr.Zero, "Ducktimizer removido. Os backups locais foram preservados.", appName, 0x40); }
    catch (Exception ex) { MessageBox(IntPtr.Zero, ex.Message, appName + " — erro", 0x10); Environment.ExitCode = 1; }
}

void CreateShortcuts()
{
    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Ducktimizer.lnk"), appPath, appName);
    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Ducktimizer.lnk"), appPath, appName);
}

void CreateShortcut(string path, string target, string description)
{
    var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
    dynamic shortcut = shell.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path })!;
    shortcut.TargetPath = target; shortcut.WorkingDirectory = installDir; shortcut.Description = description; shortcut.IconLocation = target + ",0"; shortcut.Save();
}

[DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
