namespace Altinn.Correspondence.Integrations.Hangfire;

public static class BackgroundJobContext
{
    private static readonly AsyncLocal<string?> _origin = new();

    public static string? Origin
    {
        get => _origin.Value;
        set => _origin.Value = value;
    }
}


