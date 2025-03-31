using Microsoft.Extensions.Hosting;

namespace Altinn.Correspondence.Core.Options;

public class SlackSettings
{
    private readonly IHostEnvironment _hostEnvironment;

    public SlackSettings(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public SlackSettings()
    {
        // Parameterless constructor for options binding
    }

    public string NotificationChannel => _hostEnvironment.IsProduction() ? "#mf-varsling-critical" : "#test-varslinger";

    public string? SlackUrl { get; set; }

    public string ProductionChannel { get; set; } = "#mf-varsling-critical";
    public string DevelopmentChannel { get; set; } = "#test-varslinger";
} 