using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PatoLizoOptimizer;

public static class Telemetry
{
    private const string Endpoint = "https://fluffy-octo-palm-tree.guilhermedasilvabertoso.workers.dev/api/telemetry/errors";

    public static void Report(Exception exception, string context, string kind = "error")
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                var payload = JsonSerializer.Serialize(new { kind, context, message = exception.Message, appVersion = typeof(Telemetry).Assembly.GetName().Version?.ToString() });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                await client.PostAsync(Endpoint, content);
            }
            catch { }
        });
    }
}
