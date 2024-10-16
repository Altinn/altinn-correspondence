using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Tokens;
using ScottBrady.IdentityModel;

namespace Altinn.Correspondence.API.Helpers
{
    public class EdDsaSecurityKeysCacheService : IHostedService, IDisposable
    {
        public static List<EdDsaSecurityKey> EdDsaSecurityKeys => _keys;
        private static volatile List<EdDsaSecurityKey> _keys = new();

        private PeriodicTimer? _timer;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EdDsaSecurityKeysCacheService> _logger;

        private readonly TimeSpan _refreshInterval = TimeSpan.FromHours(12);

        // In this service we allow keys for all non-production environments for
        // simplicity. Usually one would only allow a single environment (issuer) here,
        // which we could get from an injected IConfiguration/IOptions
        private readonly List<string> _wellKnownEndpoints =
        [
            //"https://localhost:7214/api/v1/.well-known/jwks.json",
            "https://altinn-dev-api.azure-api.net/dialogporten/api/v1/.well-known/jwks.json",
        "https://platform.tt02.altinn.no/dialogporten/api/v1/.well-known/jwks.json"
        ];

        public EdDsaSecurityKeysCacheService(IHttpClientFactory httpClientFactory, ILogger<EdDsaSecurityKeysCacheService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                _timer = new PeriodicTimer(_refreshInterval);
                while (await _timer.WaitForNextTickAsync(cancellationToken))
                {
                    try
                    {
                        await RefreshAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while refreshing the EdDsa keys.");
                    }
                }
            }, cancellationToken);

            await RefreshAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var keys = new List<EdDsaSecurityKey>();

            foreach (var endpoint in _wellKnownEndpoints)
            {
                try
                {
                    var response = await httpClient.GetStringAsync(endpoint, cancellationToken);
                    var jwks = new JsonWebKeySet(response);
                    foreach (var jwk in jwks.Keys)
                    {
                        if (ExtendedJsonWebKeyConverter.TryConvertToEdDsaSecurityKey(jwk, out var edDsaKey))
                        {
                            keys.Add(edDsaKey);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve keys from {endpoint}", endpoint);
                }
            }

            _logger.LogInformation("Refreshed EdDsa keys cache with {count} keys", keys.Count);

            var newKeys = keys.ToList();
            _keys = newKeys; // Atomic replace
        }
    }
}
