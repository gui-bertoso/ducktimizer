using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args.Length < 4)
{
    Console.Error.WriteLine("Uso: LicenseGenerator <private.pem> <cliente> <device-id> <dias|vitalicia>");
    return 2;
}

using var signer = ECDsa.Create();
signer.ImportFromPem(File.ReadAllText(args[0]));
var expires = args[3].Equals("vitalicia", StringComparison.OrdinalIgnoreCase)
    ? DateTime.MaxValue : DateTime.UtcNow.AddDays(int.Parse(args[3]));
var payload = JsonSerializer.SerializeToUtf8Bytes(new { Customer = args[1], DeviceId = args[2].ToUpperInvariant(), ExpiresUtc = expires, Edition = "Pro" });
var signature = signer.SignData(payload, HashAlgorithmName.SHA256);
Console.WriteLine($"{Encode(payload)}.{Encode(signature)}");
return 0;

static string Encode(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
