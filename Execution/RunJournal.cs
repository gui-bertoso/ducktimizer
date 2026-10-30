using System.Text.Json;
using Ducktimizer.Models;
using Ducktimizer.Platform;

namespace Ducktimizer.Execution;

public sealed class RunJournal(string path)
{
    public string Path { get; } = path;

    public void Save(RunReport report) => SaveValue(report);

    public void SaveSnapshot(SystemSnapshot snapshot) => SaveValue(snapshot);

    private void SaveValue<T>(T report)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        var temporary = Path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, report, WindowsPlatform.Json);
            stream.Flush(true);
        }
        File.Move(temporary, Path, true);
    }
}
