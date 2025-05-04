using Altinn.Correspondence.Application.CheckNotification;
using Altinn.Correspondence.Application.DownloadAttachment;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceHistory;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.InitializeServiceOwner;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Application.PurgeAttachment;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Notifications.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Application;

public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        // Attachment
        services.AddScoped<InitializeAttachmentHandler>();
        services.AddScoped<UploadAttachmentHandler>();
        services.AddScoped<GetAttachmentDetailsHandler>();
        services.AddScoped<GetAttachmentOverviewHandler>();
        services.AddScoped<DownloadAttachmentHandler>();
        services.AddScoped<PurgeAttachmentHandler>();

        // Correspondence
        services.AddScoped<InitializeCorrespondencesHandler>();
        services.AddScoped<PublishCorrespondenceHandler>();
        services.AddScoped<GetCorrespondencesHandler>();
        services.AddScoped<GetCorrespondenceDetailsHandler>();
        services.AddScoped<GetCorrespondenceOverviewHandler>();
        services.AddScoped<UpdateCorrespondenceStatusHandler>();
        services.AddScoped<DownloadCorrespondenceAttachmentHandler>();
        services.AddScoped<PurgeCorrespondenceHandler>();
        services.AddScoped<MigrateCorrespondenceHandler>();

        // Serviceowner
        services.AddScoped<InitializeServiceOwnerHandler>();

        // Integrations
        services.AddScoped<MalwareScanResultHandler>();
        services.AddScoped<CheckNotificationHandler>();
        services.AddScoped<ProcessLegacyPartyHandler>();

        // Helpers
        services.AddScoped<AttachmentHelper>();
        services.AddScoped<UserClaimsHelper>();
        services.AddScoped<InitializeCorrespondenceHelper>();
        services.AddScoped<UpdateCorrespondenceStatusHelper>();
        services.AddScoped<PurgeCorrespondenceHelper>();
        services.AddScoped<MobileNumberHelper>();
        services.AddScoped<HangfireScheduleHelper>();

        // Legacy
        services.AddScoped<LegacyGetCorrespondencesHandler>();
        services.AddScoped<LegacyGetCorrespondenceOverviewHandler>();
        services.AddScoped<LegacyGetCorrespondenceHistoryHandler>();
        services.AddScoped<LegacyDownloadCorrespondenceAttachmentHandler>();
        services.AddScoped<LegacyUpdateCorrespondenceStatusHandler>();
        services.AddScoped<LegacyPurgeCorrespondenceHandler>();

        // Migration
        services.AddScoped<MigrateInitializeAttachmentHandler>();
        services.AddScoped<MigrateUploadAttachmentHandler>();
        services.AddScoped<MigrateCorrespondenceHandler>();
    }
}
