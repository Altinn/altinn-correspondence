using Moq;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using System.Text;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Altinn.Correspondence.Core.Options;
using System.Net;
using Altinn.Correspondence.Common.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Moq.Protected;

namespace Altinn.Correspondence.Tests.TestingFeature
{
    public class ResourceRightsServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly Mock<IHybridCacheWrapper> _mockCache;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<ResourceRegistryService>> _mockLogger;
        private readonly Mock<IOptions<AltinnOptions>> _mockOptions;
        private readonly ResourceRegistryService _service;

        public ResourceRightsServiceTests()
        {
            _mockHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            SetupMessageHandler();
            _httpClient = new HttpClient(_mockHandler.Object);
            _mockCache = new Mock<IHybridCacheWrapper>();
            _mockLogger = new Mock<ILogger<ResourceRegistryService>>();
            _mockOptions = new Mock<IOptions<AltinnOptions>>();
            
            _mockOptions.Setup(o => o.Value).Returns(new AltinnOptions { PlatformGatewayUrl = "https://example.com" });

            _service = new ResourceRegistryService(_httpClient, _mockOptions.Object, _mockLogger.Object, _mockCache.Object);
        }

        private void SetupMessageHandler()
        {
            var TestResourceResponse = CreateTestResourceResponse();
            var serializedMockResponse = JsonSerializer.Serialize(TestResourceResponse);
            var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(serializedMockResponse, Encoding.UTF8, "application/json")
            };
            _mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get && 
                        req.RequestUri != null && req.RequestUri.AbsoluteUri == "https://example.com/resourceregistry/api/v1/resource/12345"), 
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(mockHttpResponse);
        }

        private static GetResourceResponse CreateTestResourceResponse()
        {
            return new GetResourceResponse
            {
                Identifier = "12345",
                Title = new Dictionary<string, string> { { "en", "Resource Title" } },
                Description = new Dictionary<string, string> { { "en", "Resource Description" } },
                HasCompetentAuthority = new HasCompetentAuthority
                {
                    Organization = "Org Name",
                    Orgcode = "123",
                    Name = new Dictionary<string, string> { { "en", "John Doe from API" } }
                },
                ResourceType = "SampleResourceType"
            };
        }

        private void MockSetupSimulateCacheMiss(string key, CancellationToken cancellationToken)
        {
            _mockCache.Setup(cache => cache.GetOrCreateAsync(
                key,
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(null as byte[]); 
        }

        private void MockSetupSimulateStoreInCache(string key, string serializedValue, CancellationToken cancellationToken)
        {
            _mockCache.Setup(cache => cache.GetOrCreateAsync(
                key,
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                null, // No tags
                cancellationToken
            )).ReturnsAsync(Encoding.UTF8.GetBytes(serializedValue));
        }

        private void MockSetupSimulateCacheHit(string key, string serializedValue, CancellationToken cancellationToken)
        {
            _mockCache.Setup(cache => cache.GetOrCreateAsync(
                key,
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()
            )).ReturnsAsync(Encoding.UTF8.GetBytes(serializedValue));
        }

        [Fact]
        public async Task GetServiceOwnerOfResource_ShouldCallApi_WhenCacheDoesNotExist()
        {
            // Arrange
            string resourceId = "12345";
            string cacheKey = $"ResourceInfo_{resourceId}";
            string expectedResult = CreateTestResourceResponse().HasCompetentAuthority?.Name?["en"] ?? string.Empty;
            string expectedResultSerialized = JsonSerializer.Serialize(expectedResult);
            var cancellationToken = CancellationToken.None;

            MockSetupSimulateCacheMiss(cacheKey, cancellationToken);
            MockSetupSimulateStoreInCache(cacheKey, expectedResultSerialized, cancellationToken);

            // Act
            var result = await _service.GetServiceOwnerOfResource(resourceId, cancellationToken);

            // Assert
            Assert.Equal(expectedResult, result);
            _mockHandler.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
            _mockCache.Verify(cache => cache.GetOrCreateAsync(
                    cacheKey, // Only verifying the key
                    It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                    It.IsAny<HybridCacheEntryOptions>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetServiceOwnerOfResource_ShouldRetrieveFromCache_WhenCacheExists()
        {
            // Arrange
            string resourceId = "12345";
            string cacheKey = $"ResourceInfo_{resourceId}";
            var cachedValue = new GetResourceResponse()
            {
                Title = new Dictionary<string, string> { { "en", "Resource Title" } },
                Description = new Dictionary<string, string> { { "en", "Resource Description" } },
                HasCompetentAuthority = new HasCompetentAuthority
                {
                    Organization = "991825827",
                    Orgcode = "TTD",
                    Name = new Dictionary<string, string> { { "en", "John Doe from cache" } }
                },
                Identifier = "12345",
                ResourceType = "CorrespondenceService"
            };
            var serializedData = JsonSerializer.Serialize(cachedValue);
            var cancellationToken = CancellationToken.None;

            MockSetupSimulateCacheHit(cacheKey, serializedData, cancellationToken);

            // Act
            var result = await _service.GetServiceOwnerOfResource(resourceId, cancellationToken);

            // Assert
            _mockCache.Verify(cache => cache.GetOrCreateAsync(
                    cacheKey,
                    It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                    It.IsAny<HybridCacheEntryOptions>(), 
                    It.IsAny<IEnumerable<string>?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);            _mockHandler.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task GetServiceOwnerOfResource_ShouldNotStoreInCache_WhenApiCallFails()
        {
            // Arrange
            string resourceId = "12345";
            string cacheKey = $"ResourceInfo_{resourceId}";
            var cancellationToken = CancellationToken.None;

            _mockHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

            // Act
            var result = await _service.GetServiceOwnerOfResource(resourceId, cancellationToken);

            // Assert
            Assert.Null(result);
            _mockCache.Verify(cache => cache.GetOrCreateAsync(
                cacheKey,
                It.IsAny<Func<CancellationToken, ValueTask<byte[]>>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }
    }
}