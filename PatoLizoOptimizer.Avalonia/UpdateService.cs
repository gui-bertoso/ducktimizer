using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public sealed record UpdateManifest(string Version, string Notes, string DownloadUrl, string Sha256, string Signature);

public static class UpdateService
{
    private const string ManifestUrl = "https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/api/version";

    public static async Task<UpdateManifest?> CheckAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var manifest = await client.GetFromJsonAsync<UpdateManifest>(ManifestUrl);
            if (manifest is null || !Version.TryParse(manifest.Version, out var latest)) return null;
            if (!VerifyManifest(manifest)) throw new InvalidDataException("Manifesto de atualização inválido.");
            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);
            return latest > current ? manifest : null;
        }
        catch (Exception ex) { AppLogger.Info($"Verificação de atualização indisponível: {ex.Message}"); return null; }
    }

    public static async Task<string> DownloadAsync(UpdateManifest manifest, IProgress<int>? progress = null)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        using var response = await client.GetAsync(manifest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var expectedHash = manifest.Sha256;
        var headerHash = response.Headers.TryGetValues("x-installer-sha256", out var values) ? string.Join("", values).Trim() : "";
        if (expectedHash.Length != 64 || !expectedHash.Equals(headerHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O servidor não confirmou o hash assinado da atualização.");
        var path = Path.Combine(Path.GetTempPath(), $"Ducktimizer-Setup-{manifest.Version}-win-x64.exe");
        await using (var input = await response.Content.ReadAsStreamAsync())
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long readTotal = 0;
            var total = response.Content.Headers.ContentLength;
            int read;
            while ((read = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read));
                readTotal += read;
                if (total > 0) progress?.Report((int)(readTotal * 100 / total.Value));
            }
        }
        await using var verificationStream = File.OpenRead(path);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(verificationStream));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) { File.Delete(path); throw new InvalidDataException("A atualização baixada falhou na verificação de integridade."); }
        return path;
    }

    public static void LaunchInstaller(string path) => Process.Start(new ProcessStartInfo(path, "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /NORESTART") { UseShellExecute = true });

    public static bool VerifyManifest(UpdateManifest manifest)
    {
        try
        {
            var message = System.Text.Encoding.UTF8.GetBytes($"{manifest.Version}|{manifest.Sha256}|{manifest.DownloadUrl}");
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(LicenseService.VerificationPublicKey);
            var signature = Decode(manifest.Signature);
            return verifier.VerifyData(message, signature, HashAlgorithmName.SHA256);
        }
        catch { return false; }
    }

    public static async Task<bool> SelfTestAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var manifest = await client.GetFromJsonAsync<UpdateManifest>(ManifestUrl);
        return manifest is not null && VerifyManifest(manifest) && manifest.Sha256.Length == 64;
    }

    private static byte[] Decode(string text)
    {
        text = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
    }
}
