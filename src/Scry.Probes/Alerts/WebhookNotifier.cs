using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scry.Core;
using Scry.Probes.Internal;

namespace Scry.Probes.Alerts;

internal sealed class WebhookNotifier : IAlertNotifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotifier> _logger;

    public string Kind => "webhook";

    public WebhookNotifier(IHttpClientFactory httpClientFactory, ILogger<WebhookNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(AlertRule rule, AlertEvent evt, ProbeResult result, CancellationToken ct)
    {
        if (rule.NotifierConfig is null)
        {
            return;
        }

        var config = YamlConfig.Deserialize<WebhookNotifierConfig>(rule.NotifierConfig);
        using var http = _httpClientFactory.CreateClient("scry.alerts");
        using var request = new HttpRequestMessage(new HttpMethod(config.Method), config.Url);

        foreach (var (key, value) in config.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        var payload = new
        {
            alertRuleId = rule.Id,
            alertName = rule.Name,
            severity = evt.Severity.ToString(),
            state = evt.State.ToString(),
            probeId = result.ProbeId,
            workspaceId = result.WorkspaceId,
            outcome = result.Outcome.ToString(),
            message = result.Message,
            completedAt = result.CompletedAt,
        };

        request.Content = JsonContent.Create(payload, options: JsonOptions);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook {Url} returned {Status} for alert {AlertName}",
                    config.Url, (int)response.StatusCode, rule.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook delivery failed for alert {AlertName}", rule.Name);
        }
    }
}
