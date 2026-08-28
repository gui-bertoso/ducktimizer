using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PatoLizoOptimizer;

public sealed record LicensePayload(string Customer, string DeviceId, DateTime ExpiresUtc, string Edition);

public static class LicenseService
{
    private const string PublicKey = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEa4QwqBHmllMJa32suaCUTxSLBusT
niu4cPDKM0rosIQ6zj1e4yAO65gvglTrNTCBoIGY+mG6VLcltOSGY8KM3A==
-----END PUBLIC KEY-----
""";

    private static readonly string LicensePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Ducktimizer", "license.key");

    public static string DeviceId
    {
        get
        {
            var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "")?.ToString() ?? "unknown";
            var material = $"DUCKTIMIZER|{machineGuid}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..24];
            return string.Join('-', hash.Chunk(4).Select(x => new string(x)));
        }
    }

    public static bool IsActivated(out LicensePayload? license, out string reason)
    {
        license = null;
        if (!File.Exists(LicensePath)) { reason = "Licença não encontrada."; return false; }
        return Validate(File.ReadAllText(LicensePath).Trim(), out license, out reason);
    }

    public static bool Activate(string token, out LicensePayload? license, out string reason)
    {
        if (!Validate(token.Trim(), out license, out reason)) return false;
        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
        File.WriteAllText(LicensePath, token.Trim());
        return true;
    }

    public static bool Validate(string token, out LicensePayload? license, out string reason)
    {
        license = null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) throw new FormatException();
            var payloadBytes = Decode(parts[0]);
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(PublicKey);
            if (!verifier.VerifyData(payloadBytes, Decode(parts[1]), HashAlgorithmName.SHA256))
            { reason = "Assinatura da licença inválida."; return false; }
            license = JsonSerializer.Deserialize<LicensePayload>(payloadBytes) ?? throw new FormatException();
            if (!string.Equals(license.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase))
            { reason = "Esta licença pertence a outro computador."; return false; }
            if (license.ExpiresUtc != DateTime.MaxValue && license.ExpiresUtc.ToUniversalTime() < DateTime.UtcNow)
            { reason = "Esta licença expirou."; return false; }
            reason = "Licença válida.";
            return true;
        }
        catch { reason = "Formato de licença inválido."; return false; }
    }

    private static byte[] Decode(string text)
    {
        text = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(text.PadRight(text.Length + (4 - text.Length % 4) % 4, '='));
    }
}
