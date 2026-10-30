using System.Diagnostics;
using System.Security.Principal;

namespace Ducktimizer.Platform;

public static class WindowsAccess
{
    public static bool IsAdministrator
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public static bool RestartElevated()
    {
        if (!OperatingSystem.IsWindows())
            return false;
        using var identity = WindowsIdentity.GetCurrent();
        var start = new ProcessStartInfo(
            Environment.ProcessPath ?? throw new IOException("Executable path is unavailable."))
        {
            UseShellExecute = true,
            Verb = "runas"
        };
        start.ArgumentList.Add("--analyze");
        start.ArgumentList.Add("--owner-sid");
        start.ArgumentList.Add(identity.User?.Value ??
                               throw new InvalidOperationException("The current user SID is unavailable."));
        using var process = Process.Start(start);
        return process is not null;
    }

    public static void ValidateOwner(string[] args)
    {
        if (!OperatingSystem.IsWindows() || !args.Contains("--owner-sid"))
            return;
        var index = Array.IndexOf(args, "--owner-sid");
        using var identity = WindowsIdentity.GetCurrent();
        if (index + 1 >= args.Length || args[index + 1] != identity.User?.Value)
            throw new InvalidOperationException(
                "Use administrator access for the same Windows account. Running as another account would " +
                "change that account's settings instead.");
    }
}
