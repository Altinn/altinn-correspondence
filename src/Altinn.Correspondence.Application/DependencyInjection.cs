using Altinn.Correspondence.Application.GetAttachmentDetails;
using Altinn.Correspondence.Application.GetAttachmentOverview;
using Altinn.Correspondence.Application.GetCorrespondenceDetails;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using Altinn.Correspondence.Application.GetCorrespondences;
using Altinn.Correspondence.Application.DownloadAttachment;
using Altinn.Correspondence.Application.InitializeAttachment;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Altinn.Correspondence.Application.UploadAttachment;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Correspondence.Application.PurgeAttachment;

namespace Altinn.Correspondence.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeAttachmentHandler>();
        services.AddScoped<InitializeCorrespondenceHandler>();
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
    }
}
