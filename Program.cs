using Avalonia;
using Ducktimizer.Platform;

namespace Ducktimizer;

internal static class Program
{
    [STAThread]

    public static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.ExitCode = SelfTests.RunAsync().GetAwaiter().GetResult();
            return;
        }
        try
        {
            WindowsAccess.ValidateOwner(args);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            AppLogger.Error(error, "Account check");
            Environment.ExitCode = 1;
            return;
        }
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLogger.Error((Exception)e.ExceptionObject, "Unhandled failure");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
