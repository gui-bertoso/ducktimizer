using Avalonia;
using System;
using System.Threading;

namespace PatoLizoOptimizer;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            Environment.ExitCode = OptimizerEngine.SelfTest() ? 0 : 1;
            return;
        }
        if (args.Length > 0 && (args[0] == "--apply" || args[0] == "--undo-all"))
        {
            Environment.ExitCode = OptimizerEngine.RunElevated(args);
            return;
        }
        using var mutex = new Mutex(true, @"Local\Ducktimizer.SingleInstance", out var firstInstance);
        if (!firstInstance) return;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { var exception = (Exception)e.ExceptionObject; AppLogger.Error(exception, "Falha não tratada"); };
        AppLogger.Info("Ducktimizer iniciado");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
