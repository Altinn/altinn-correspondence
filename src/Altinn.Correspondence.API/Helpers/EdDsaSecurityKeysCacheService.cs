using Microsoft.IdentityModel.Tokens;
using ScottBrady.IdentityModel.Tokens;
using ScottBrady.IdentityModel;
using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.API.Helpers
{
    public class EdDsaSecurityKeysCacheService : IHostedService, IDisposable
    {
        public static List<EdDsaSecurityKey> EdDsaSecurityKeys => _keys;
        private static volatile List<EdDsaSecurityKey> _keys = new();

        private PeriodicTimer? _timer;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EdDsaSecurityKeysCacheService> _logger;
        private readonly DialogportenSettings _dialogportenSettings;

        private readonly TimeSpan _refreshInterval = TimeSpan.FromHours(12);
        public EdDsaSecurityKeysCacheService(IHttpClientFactory httpClientFactory, IOptions<DialogportenSettings> dialogportenSettings, ILogger<EdDsaSecurityKeysCacheService> logger)
        {
            _dialogportenSettings = dialogportenSettings.Value;
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

            var endpoint = _dialogportenSettings.Issuer + "/.well-known/jwks.json";
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

            _logger.LogInformation("Refreshed EdDsa keys cache with {count} keys", keys.Count);

            var newKeys = keys.ToList();
            _keys = newKeys; // Atomic replace
        }
    }
}
