using Altinn.Correspondence.Application.CleanupBruksmonster;
using Altinn.Correspondence.Application.GenerateReport;
using Altinn.Correspondence.Application.IpSecurityRestrictionsUpdater;
using Altinn.Correspondence.Application.MaskinportenJwkRotation;
using Altinn.Correspondence.Core.Options;
using Hangfire;

namespace Altinn.Correspondence.API.Helpers;

public static class RecurringJobRegistration
{
    public const string MaskinportenJwkRotationJobId = "Rotate Maskinporten JWK and update Key Vault";

    public static void Register(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var recurringJobManager = services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<IpSecurityRestrictionUpdater>(
            "Update IP restrictions to apimIp and current EventGrid IPs",
            handler => handler.UpdateIpRestrictions(),
            Cron.Daily());

        recurringJobManager.AddOrUpdate<GenerateDailySummaryReportHandler>(
            "Generate daily summary report",
            handler => handler.Process(new GenerateDailySummaryReportRequest { Altinn2Included = false }, CancellationToken.None),
            Cron.Daily());

        recurringJobManager.AddOrUpdate<CleanupBruksmonsterHandler>(
            "Cleanup bruksmonster test data older than 1 day",
            handler => handler.Process(new CleanupBruksmonsterRequest { MinAgeDays = 1 }, null, CancellationToken.None),
            Cron.Daily());

        var settings = configuration.GetSection(nameof(MaskinportenJwkRotationSettings)).Get<MaskinportenJwkRotationSettings>()
            ?? new MaskinportenJwkRotationSettings();

        if (!settings.Enabled)
        {
            recurringJobManager.RemoveIfExists(MaskinportenJwkRotationJobId);
            logger.LogInformation("Maskinporten JWK rotation job is disabled.");
            return;
        }

        recurringJobManager.AddOrUpdate<MaskinportenJwkRotationHandler>(
            MaskinportenJwkRotationJobId,
            handler => handler.Process(CancellationToken.None),
            settings.CronExpression);

        logger.LogInformation("Maskinporten JWK rotation job registered with cron {CronExpression}.", settings.CronExpression);
    }
}
