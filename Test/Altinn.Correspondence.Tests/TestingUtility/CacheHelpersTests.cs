using Moq;
using Microsoft.Extensions.Caching.Distributed;
using Altinn.Correspondence.Common.Helpers;
using System.Text.Json;
using System.Text;

namespace Altinn.Correspondence.Tests.TestingUtility
{
    public class CacheHelpersTests
    {
        [Fact]
        public async Task StoreObjectInCacheAsync_ShouldStoreSerializedObjectInCache()
        {
            // Arrange
            var key = "myKey";
            Dictionary<string, object> value = new()
            {
                { "Name", "John" },
                { "Age", 30 }
            };
            var cacheOptions = new DistributedCacheEntryOptions();
            var cancellationToken = CancellationToken.None;
            var serializedDataString = JsonSerializer.Serialize(value);
            var mockCache = new Mock<IDistributedCache>();

            mockCache.Setup(cache => cache.SetAsync(
                key,
                It.Is<byte[]>(bytes => bytes != null && bytes.Length > 0),
                cacheOptions,
                cancellationToken
            )).Returns(Task.CompletedTask);

            // Act
            await CacheHelpers.StoreObjectInCacheAsync(key, value, mockCache.Object, cacheOptions, cancellationToken);

            // Assert
            mockCache.Verify(cache => cache.SetAsync(
                key, 
                It.Is<byte[]>(bytes => bytes.SequenceEqual(Encoding.UTF8.GetBytes(serializedDataString))),
                cacheOptions, 
                cancellationToken),
                Times.Once);
        }

        [Fact]
        public async Task GetObjectFromCacheAsync_ShouldReturnDeserializedObject_WhenDataIsFound()
        {
            // Arrange
            var key = "myKey";
            string storedValue = "John";
            var serializedData = JsonSerializer.Serialize(storedValue);
            var cancellationToken = CancellationToken.None;
            var mockCache = new Mock<IDistributedCache>();
            
            mockCache.Setup(cache => cache.GetAsync(key, cancellationToken))
                .ReturnsAsync(Encoding.UTF8.GetBytes(serializedData));

            // Act
            var result = await CacheHelpers.GetObjectFromCacheAsync<string>(key, mockCache.Object, cancellationToken);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(storedValue, result);
        }

        [Fact]
        public async Task GetObjectFromCacheAsync_ShouldReturnNull_WhenDataIsNotFound()
        {
            // Arrange
            var key = "unknownKey";
            var cancellationToken = CancellationToken.None;
            var mockCache = new Mock<IDistributedCache>();

            mockCache.Setup(cache => cache.GetAsync(key, cancellationToken))
                .ReturnsAsync(Array.Empty<byte>());

            // Act
            var result = await CacheHelpers.GetObjectFromCacheAsync<object>(key, mockCache.Object, cancellationToken);

            // Assert
            Assert.Null(result);
        }
    }
}