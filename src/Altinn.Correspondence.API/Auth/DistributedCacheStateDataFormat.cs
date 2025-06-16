using Altinn.Correspondence.Common.Caching;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Altinn.Correspondence.API.Auth
{
    public class DistributedCacheStateDataFormat : ISecureDataFormat<AuthenticationProperties>
    {
        private readonly IHybridCacheWrapper _cache;
        private readonly string _keyPrefix;

        public DistributedCacheStateDataFormat(IHybridCacheWrapper cache, string keyPrefix)
        {
            _cache = cache;
            _keyPrefix = keyPrefix;
        }

        public string Protect(AuthenticationProperties data)
        {
            var key = $"{_keyPrefix}_{Guid.NewGuid()}";
            var json = JsonSerializer.Serialize(data.Items);
            _cache.SetAsync(key, json, new Microsoft.Extensions.Caching.Hybrid.HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(15)
            }).GetAwaiter().GetResult();
            return key;
        }

        public string Protect(AuthenticationProperties data, string purpose)
        {
            return Protect(data);
        }

        public AuthenticationProperties Unprotect(string protectedText)
        {
            var json = _cache.GetAsync<string>(protectedText).GetAwaiter().GetResult();
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var items = JsonSerializer.Deserialize<IDictionary<string, string>>(json);
            return new AuthenticationProperties(items);
        }

        public AuthenticationProperties Unprotect(string protectedText, string purpose)
        {
            return Unprotect(protectedText);
        }
    }
} 