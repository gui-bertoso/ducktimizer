using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Ducktimizer.Platform;

public sealed class PowerShellProcess
{
    // Keep the process command line short. The full script and JSON payload travel over stdin.

    private const string Bootstrap = "[Console]::InputEncoding=[Text.UTF8Encoding]::new($false); " +
                                     "$code=[Console]::In.ReadToEnd(); & ([scriptblock]::Create($code))";

    public async Task<string> RunAsync(string script, TimeSpan? timeout = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows 11 is required. No changes were made.");
        var start =
            new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                     Convert.ToBase64String(Encoding.Unicode.GetBytes(Bootstrap)) })
            start.ArgumentList.Add(argument);
        using var process =
            Process.Start(start) ?? throw new IOException("Could not start Windows PowerShell.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(3));
        try
        {
            var code = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; " +
                       "[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);\n" +
                       script;
            await process.StandardInput.WriteAsync(code.AsMemory(), deadline.Token);
            await process.StandardInput.FlushAsync(deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (Exception failure) when (failure is OperationCanceledException or IOException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (Exception killFailure) when (killFailure is InvalidOperationException or Win32Exception)
            {
            }
            if (failure is OperationCanceledException)
                throw new TimeoutException(
                    "Windows operation timed out. Its final state is unknown; analyze again before retrying.",
                    failure);
            throw new IOException("Communication with Windows PowerShell failed. The operation may be " +
                                  "incomplete; analyze again before retrying.",
                failure);
        }
        var stdout = await output;
        var stderr = await error;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                                                    ? $"Windows operation failed ({process.ExitCode})."
                                                    : stderr.Trim());
        return stdout.Trim();
    }
}
