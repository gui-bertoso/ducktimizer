using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public sealed record LicensePayload(string Customer, string DeviceId, DateTime ExpiresUtc, string Edition, string? LicenseId = null);
public sealed record LicenseStatusResponse(bool Active, string LeaseUntil, string? Signature);
public sealed record CachedLease(string LicenseId, string DeviceId, string LeaseUntil, string Signature);

public static class LicenseService
{
    private const string ActivationUrl = "https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/api/activate";
    private const string StatusUrl = "https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/api/license/status";
    internal const string VerificationPublicKey = """
-----BEGIN PUBLIC KEY-----
MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEa4QwqBHmllMJa32suaCUTxSLBusT
niu4cPDKM0rosIQ6zj1e4yAO65gvglTrNTCBoIGY+mG6VLcltOSGY8KM3A==
-----END PUBLIC KEY-----
""";

    private static readonly string LicensePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Ducktimizer", "license.key");
    private static readonly string LeasePath = Path.Combine(Path.GetDirectoryName(LicensePath)!, "license.lease");

    public static string DeviceId
    {
        get
        {
            var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "")?.ToString();
            if (string.IsNullOrWhiteSpace(machineGuid))
            {
                var fallbackPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Ducktimizer", "device.id");
                Directory.CreateDirectory(Path.GetDirectoryName(fallbackPath)!);
                machineGuid = File.Exists(fallbackPath) ? File.ReadAllText(fallbackPath).Trim() : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                if (!File.Exists(fallbackPath)) File.WriteAllText(fallbackPath, machineGuid);
            }
            var material = $"DUCKTIMIZER|{machineGuid}";
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..24];
            return string.Join('-', hash.Chunk(4).Select(x => new string(x)));
        }
    }

    public static bool IsActivated(out LicensePayload? license, out string reason)
    {
        license = null;
        if (!File.Exists(LicensePath)) { reason = "Licença não encontrada."; return false; }
        try { return Validate(File.ReadAllText(LicensePath).Trim(), out license, out reason); }
        catch (Exception) { reason = "Não foi possível ler a licença instalada."; return false; }
    }

    public static async Task<(bool Success, string Reason)> CheckStatusAsync(LicensePayload license)
    {
        if (string.IsNullOrWhiteSpace(license.LicenseId)) return (true, "Licença legada válida.");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await client.PostAsJsonAsync(StatusUrl, new { licenseId = license.LicenseId, deviceId = DeviceId });
            var status = await response.Content.ReadFromJsonAsync<LicenseStatusResponse>();
            if (status is { Active: true } && VerifyLease(license.LicenseId, DeviceId, status.LeaseUntil, status.Signature ?? ""))
            {
                File.WriteAllText(LeasePath, JsonSerializer.Serialize(new CachedLease(license.LicenseId, DeviceId, status.LeaseUntil, status.Signature!)));
                return (true, "Licença online válida.");
            }
            if (status is { Active: false }) return (false, "Esta licença foi suspensa ou não corresponde a este computador.");
        }
        catch (Exception ex) { AppLogger.Info($"Validação online indisponível: {ex.Message}"); }
        return CachedLeaseValid(license.LicenseId) ? (true, "Validação temporária offline.") : (false, "Conecte-se à internet para renovar a validação da licença.");
    }

    private static bool CachedLeaseValid(string licenseId)
    {
        try
        {
            var lease = JsonSerializer.Deserialize<CachedLease>(File.ReadAllText(LeasePath));
            return lease is not null && lease.LicenseId == licenseId && lease.DeviceId == DeviceId && DateTime.TryParse(lease.LeaseUntil, null, System.Globalization.DateTimeStyles.RoundtripKind, out var until) && until.ToUniversalTime() > DateTime.UtcNow && VerifyLease(lease.LicenseId, lease.DeviceId, lease.LeaseUntil, lease.Signature);
        }
        catch { return false; }
    }

    private static bool VerifyLease(string licenseId, string deviceId, string until, string signature)
    {
        try
        {
            var message = Encoding.UTF8.GetBytes($"{licenseId}|{deviceId}|{until}");
            using var verifier = ECDsa.Create(); verifier.ImportFromPem(VerificationPublicKey);
            return verifier.VerifyData(message, Decode(signature), HashAlgorithmName.SHA256);
        }
        catch { return false; }
    }

    public static async Task<(bool Success, LicensePayload? License, string Reason)> ActivateAsync(string key)
    {
        key = key.Trim();
        if (Validate(key, out var existing, out _))
        {
            Save(key);
            return (true, existing, "Licença ativada.");
        }
        if (!key.StartsWith("DUCK-", StringComparison.OrdinalIgnoreCase))
            return (false, null, "Informe uma chave de ativação válida.");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var response = await client.PostAsJsonAsync(ActivationUrl, new { key, deviceId = DeviceId });
            var result = await response.Content.ReadFromJsonAsync<ActivationResponse>();
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(result?.Token))
                return (false, null, result?.Message ?? "Não foi possível ativar. Verifique sua conexão.");
            if (!Validate(result.Token, out var license, out var reason)) return (false, null, reason);
            Save(result.Token);
            return (true, license, "Licença ativada.");
        }
        catch (TaskCanceledException) { return (false, null, "A ativação demorou demais. Tente novamente."); }
        catch (HttpRequestException) { return (false, null, "Sem conexão com o servidor de ativação."); }
        catch { return (false, null, "Não foi possível concluir a ativação."); }
    }

    private static void Save(string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
        File.WriteAllText(LicensePath, token.Trim());
    }

    public static bool Validate(string token, out LicensePayload? license, out string reason)
    {
        license = null;
        try
        {
            if (token.Length is < 32 or > 4096) throw new FormatException();
            var parts = token.Split('.');
            if (parts.Length != 2) throw new FormatException();
            var payloadBytes = Decode(parts[0]);
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(VerificationPublicKey);
            if (!verifier.VerifyData(payloadBytes, Decode(parts[1]), HashAlgorithmName.SHA256))
            { reason = "Assinatura da licença inválida."; return false; }
            license = JsonSerializer.Deserialize<LicensePayload>(payloadBytes) ?? throw new FormatException();
            if (license.Customer.Length is < 2 or > 80 || license.Edition != "Pro" || license.DeviceId.Length != 29) throw new FormatException();
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

public sealed record ActivationResponse(string? Token, string? Message);
