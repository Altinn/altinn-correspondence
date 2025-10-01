using Altinn.Correspondence.Application.CancelNotification;
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
using Altinn.Correspondence.Application.MigrateCorrespondence;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.InitializeServiceOwner;
using Altinn.Correspondence.Application.MigrateToStorageProvider;
using Altinn.Correspondence.Application.MigrateCorrespondenceAttachment;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Application.PurgeAttachment;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Application.ConfirmCorrespondence;
using Altinn.Correspondence.Application.MarkCorrespondenceAsRead;
using Altinn.Notifications.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Correspondence.Application.CleanupOrphanedDialogs;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Application.LegacyUpdateCorrespondenceStatus;
using Altinn.Correspondence.Application.GenerateReport;
using Altinn.Correspondence.Application.RestoreSoftDeletedDialogs;

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
        services.AddScoped<ConfirmCorrespondenceHandler>();
        services.AddScoped<MarkCorrespondenceAsReadHandler>();
        services.AddScoped<DownloadCorrespondenceAttachmentHandler>();
        services.AddScoped<PurgeCorrespondenceHandler>();

        // Serviceowner
        services.AddScoped<InitializeServiceOwnerHandler>();

        // Integrations
        services.AddScoped<MalwareScanResultHandler>();
        services.AddScoped<CheckNotificationHandler>();
        services.AddScoped<ProcessLegacyPartyHandler>();
        services.AddScoped<CancelNotificationHandler>();

        // Maintenance
        services.AddScoped<CleanupOrphanedDialogsHandler>();
        services.AddScoped<CleanupPerishingDialogs.CleanupPerishingDialogsHandler>();
        services.AddScoped<CleanupMarkdownAndHTMLInSummary.CleanupMarkdownAndHTMLInSummaryHandler>();
        services.AddScoped<RestoreSoftDeletedDialogsHandler>();

        // Statistics & Reporting
        services.AddScoped<GenerateDailySummaryReportHandler>();

        // Helpers
        services.AddScoped<AttachmentHelper>();
        services.AddScoped<UserClaimsHelper>();
        services.AddScoped<InitializeCorrespondenceHelper>();
        services.AddScoped<ServiceOwnerHelper>();
        services.AddScoped<PurgeCorrespondenceHelper>();
        services.AddScoped<MobileNumberHelper>();
        services.AddScoped<HangfireScheduleHelper>();
        services.AddScoped<NotificationMapper>();

        // Legacy
        services.AddScoped<LegacyGetCorrespondencesHandler>();
        services.AddScoped<LegacyGetCorrespondenceOverviewHandler>();
        services.AddScoped<LegacyGetCorrespondenceHistoryHandler>();
        services.AddScoped<LegacyDownloadCorrespondenceAttachmentHandler>();
        services.AddScoped<LegacyUpdateCorrespondenceStatusHandler>();
        services.AddScoped<LegacyPurgeCorrespondenceHandler>();

        // Migration
        services.AddScoped<MigrateAttachmentHelper>();
        services.AddScoped<MigrateAttachmentHandler>();
        services.AddScoped<MigrateCorrespondenceHandler>();
        services.AddScoped<MigrateToStorageProviderHandler>();

        // EventSync
        services.AddScoped<SyncCorrespondenceStatusEventHandler>();
        services.AddScoped<SyncCorrespondenceNotificationEventHandler>();
        services.AddScoped<SyncCorrespondenceForwardingEventHandler>();
    }
}
