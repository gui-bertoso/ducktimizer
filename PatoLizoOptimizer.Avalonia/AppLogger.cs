using System;
using System.IO;

namespace PatoLizoOptimizer;

public static class AppLogger
{
    private static readonly object Gate = new();
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ducktimizer", "logs");

    public static void Info(string message) => Write("INFO", message);
    public static void Error(Exception exception, string context) => Write("ERROR", $"{context}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, $"ducktimizer-{DateTime.Now:yyyy-MM}.log"),
                    $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
