using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Scry.Probes.Azure;

/// <summary>
/// Resolves an Azure TokenCredential from configuration.
/// Priority: ClientSecretCredential (TenantId + ClientId + ClientSecret in config)
///           → DefaultAzureCredential (managed identity, environment vars, VS/CLI auth)
/// </summary>
internal sealed class AzureCredentialProvider
{
    private readonly TokenCredential _credential;
    private readonly bool _isConfigured;

    public AzureCredentialProvider(IConfiguration configuration, ILogger<AzureCredentialProvider> logger)
    {
        var tenantId = configuration["Scry:Azure:TenantId"];
        var clientId = configuration["Scry:Azure:ClientId"];
        var clientSecret = configuration["Scry:Azure:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret))
        {
            _credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            _isConfigured = true;
            logger.LogInformation("Azure credentials: using ClientSecretCredential (tenant {TenantId})", tenantId);
        }
        else
        {
            // DefaultAzureCredential works with managed identity, AZURE_* env vars,
            // az CLI, Visual Studio auth — add credentials and this just works.
            _credential = new DefaultAzureCredential();
            _isConfigured = false;
            logger.LogDebug("Azure credentials: using DefaultAzureCredential (no explicit config found)");
        }
    }

    /// <summary>
    /// Whether explicit credentials (TenantId + ClientId + ClientSecret) are configured.
    /// DefaultAzureCredential may still succeed via environment or managed identity.
    /// </summary>
    public bool HasExplicitCredentials => _isConfigured;

    public TokenCredential Credential => _credential;
}
