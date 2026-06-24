using System.Net;
using Garnet;
using StackExchange.Redis;

namespace Altinn.Correspondence.API.Caching;

/// <summary>
/// Hosts a <see cref="GarnetServer"/> in-process so that local development and the test suite
/// do not depend on a separate Docker Redis container. Garnet speaks the RESP protocol, so the
/// existing StackExchange.Redis based clients (distributed cache + data protection) connect to it
/// unchanged via the configured <c>RedisConnectionString</c>.
///
/// The server is started once per process. The test suite spins up multiple
/// <c>WebApplicationFactory</c> instances in the same process, and they would otherwise race to
/// bind the same TCP port. A process-wide singleton mirrors the previously shared Docker container.
///
/// This is only intended for the Development environment. Staging/production continue to use an
/// external (managed) Redis, which is required for the cache and data protection keys to be shared
/// across multiple app instances.
/// </summary>
internal static class EmbeddedGarnetServer
{
    private static readonly object _lock = new();
    private static readonly TimeSpan StartupProbeDelay = TimeSpan.FromMilliseconds(100);
    private const int MaxStartupProbeAttempts = 50;
    private static GarnetServer? _server;

    public static void EnsureStarted(string redisConnectionString, ILogger logger)
    {
        if (_server is not null)
        {
            return;
        }

        lock (_lock)
        {
            if (_server is not null)
            {
                return;
            }

            var options = ConfigurationOptions.Parse(redisConnectionString);
            var (host, port) = ResolveEndpoint(options.EndPoints.FirstOrDefault());

            var serverArgs = new List<string>
            {
                "--bind", host,
                "--port", port.ToString(),
            };

            if (!string.IsNullOrEmpty(options.Password))
            {
                serverArgs.Add("--auth");
                serverArgs.Add("Password");
                serverArgs.Add("--password");
                serverArgs.Add(options.Password);
            }

            logger.LogInformation("Starting embedded Garnet server on {Host}:{Port}", host, port);

            var server = new GarnetServer(serverArgs.ToArray());
            server.Start();
            WaitUntilAcceptingConnections(redisConnectionString);
            _server = server;

            logger.LogInformation("Embedded Garnet server started");
        }
    }

    private static void WaitUntilAcceptingConnections(string redisConnectionString)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxStartupProbeAttempts; attempt++)
        {
            try
            {
                using var connection = ConnectionMultiplexer.Connect(redisConnectionString);
                connection.GetDatabase().Ping();
                return;
            }
            catch (Exception ex) when (IsStartupProbeException(ex))
            {
                lastException = ex;
                if (attempt < MaxStartupProbeAttempts)
                {
                    Thread.Sleep(StartupProbeDelay);
                }
            }
        }

        throw new InvalidOperationException("Embedded Garnet server did not start accepting Redis connections.", lastException);
    }

    private static bool IsStartupProbeException(Exception exception)
    {
        return exception is RedisConnectionException or RedisTimeoutException or RedisServerException;
    }

    private static (string Host, int Port) ResolveEndpoint(EndPoint? endpoint)
    {
        // Garnet's --bind expects an IP address, so map the loopback hostname to 127.0.0.1.
        switch (endpoint)
        {
            case DnsEndPoint dns:
                var host = dns.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : dns.Host;
                return (host, dns.Port);
            case IPEndPoint ip:
                return (ip.Address.ToString(), ip.Port);
            default:
                return ("127.0.0.1", 6379);
        }
    }
}
