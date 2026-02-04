namespace Altinn.Correspondence.API.Helpers;

public static class StartupAppSettingsLogging
{
    public static void LogConfigurationKeys(IConfiguration configuration, ILogger logger, bool logAllKeys)
    {
        var keys = new List<string>();
        GetConfigurationKeys(configuration, "", keys);
        logger.LogInformation("Found {configurationKeysCount} configuration keys", keys.Count);
        
        if (logAllKeys) { 
            foreach (var key in keys.OrderBy(k => k))
            {
                logger.LogDebug("  {Key}", key);
            }
        }
    }

    private static void GetConfigurationKeys(IConfiguration configuration, string prefix, List<string> keys)
    {
        foreach (var child in configuration.GetChildren())
        {
            var key = string.IsNullOrEmpty(prefix) ? child.Key : $"{prefix}:{child.Key}";
            
            // If this section has a direct value, add it
            var value = child.Value;
            if (value != null)
            {
                keys.Add(key);
            }
            
            // If it has children, recurse into them
            if (child.GetChildren().Any())
            {
                GetConfigurationKeys(child, key, keys);
            }
        }
    }
}

