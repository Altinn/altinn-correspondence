using System.Net.Http.Json;
using Altinn.Correspondence.Repositories;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Integrations.Altinn.Notifications;

public class AltinnDevNotificationService : IAltinnNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IResourceRightsService _resourceRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AltinnNotificationService> _logger;

    public AltinnDevNotificationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IHttpContextAccessor httpContextAccessor, IResourceRightsService resourceRepository, IHostEnvironment hostEnvironment, ILogger<AltinnNotificationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _resourceRepository = resourceRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<Guid?> CreateNotification(NotificationOrderRequest notification, CancellationToken cancellationToken = default)
    {
        return Guid.NewGuid();
    }

    public async Task<bool> CancelNotification(string orderId, CancellationToken cancellationToken = default)
    {
        return true;
    }
}
