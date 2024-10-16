using Altinn.Correspondence.Application.CheckNotification;
using Altinn.Correspondence.Application.DownloadAttachment;
using Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;
using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Application.PurgeAttachment;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Application.UpdateMarkAsUnread;
using Altinn.Correspondence.Application.UploadAttachment;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Application;

public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeAttachmentHandler>();
        services.AddScoped<InitializeCorrespondencesHandler>();
        services.AddScoped<GetCorrespondencesHandler>();
        services.AddScoped<GetCorrespondenceDetailsHandler>();
        services.AddScoped<GetCorrespondenceOverviewHandler>();
        services.AddScoped<GetAttachmentOverviewHandler>();
        services.AddScoped<GetAttachmentDetailsHandler>();
        services.AddScoped<UpdateCorrespondenceStatusHandler>();
        services.AddScoped<UploadAttachmentHandler>();
        services.AddScoped<DownloadAttachmentHandler>();
        services.AddScoped<PurgeAttachmentHandler>();
        services.AddScoped<MalwareScanResultHandler>();
        services.AddScoped<PurgeCorrespondenceHandler>();
        services.AddScoped<UpdateMarkAsUnreadHandler>();
        services.AddScoped<MigrateCorrespondenceHandler>();
        services.AddScoped<LegacyGetCorrespondencesHandler>();
        services.AddScoped<CheckNotificationHandler>();
        services.AddScoped<MigrateInitializeAttachmentHandler>();
        services.AddScoped<MigrateUploadAttachmentHandler>();

        services.AddScoped<InitializeCorrespondenceHelper>();
        services.AddScoped<UploadHelper>();
        services.AddScoped<UserClaimsHelper>();
        services.AddScoped<PublishCorrespondenceHandler>();
        services.AddScoped<DownloadCorrespondenceAttachmentHandler>();
    }
}
