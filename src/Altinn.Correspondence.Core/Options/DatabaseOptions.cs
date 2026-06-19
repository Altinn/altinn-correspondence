namespace Altinn.Correspondence.Core.Options;

public class DatabaseOptions
{
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for transient database failures. Defaults to 5.
    /// Set to 0 to disable retries (useful in tests to fail fast).
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;
}
