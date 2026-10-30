using System.Security.Cryptography;
using System.Text;

namespace Ducktimizer.Models;

public static class TermsNotice
{
    public const string Version = "2026-10-30.1";

    public static string Text { get; } = Read();

    public static string Sha256 { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Text))).ToLowerInvariant();

    public static TermsAcknowledgement Acknowledge() => new(Version, Sha256, DateTimeOffset.UtcNow);

    private static string Read()
    {
        using var stream = typeof(TermsNotice).Assembly.GetManifestResourceStream("TERMS.txt")
            ?? throw new FileNotFoundException("The terms and risk notice is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = reader.ReadToEnd();
        if (!text.Contains("Version: " + Version, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The embedded terms notice version is inconsistent.");
        }
        return text;
    }
}

public sealed record TermsAcknowledgement(string Version, string Sha256, DateTimeOffset AcknowledgedAt);
