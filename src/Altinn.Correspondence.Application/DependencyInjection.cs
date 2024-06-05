using Altinn.Correspondence.Application.GetAttachmentDetailsCommand;
using Altinn.Correspondence.Application.GetAttachmentOverviewCommand;
using Altinn.Correspondence.Application.GetCorrespondenceDetailsCommand;
using Altinn.Correspondence.Application.GetCorrespondenceOverviewCommand;
using Altinn.Correspondence.Application.GetCorrespondencesCommand;
using Altinn.Correspondence.Application.DownloadAttachmentQuery;
using Altinn.Correspondence.Application.InitializeAttachmentCommand;
using Altinn.Correspondence.Application.InitializeCorrespondenceCommand;
using Altinn.Correspondence.Application.UploadAttachmentCommand;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatusCommand;
using Microsoft.Extensions.DependencyInjection;
using Altinn.Correspondence.Application.PurgeAttachmentCommand;

namespace Altinn.Correspondence.Application;
public static class DependencyInjection
{
    public static void AddApplicationHandlers(this IServiceCollection services)
    {
        services.AddScoped<InitializeAttachmentCommandHandler>();
        services.AddScoped<InitializeCorrespondenceCommandHandler>();
        services.AddScoped<GetCorrespondencesCommandHandler>();
        services.AddScoped<GetCorrespondenceDetailsCommandHandler>();
        services.AddScoped<GetCorrespondenceOverviewCommandHandler>();
        services.AddScoped<GetAttachmentOverviewCommandHandler>();
        services.AddScoped<GetAttachmentDetailsCommandHandler>();
        services.AddScoped<UpdateCorrespondenceStatusCommandHandler>();
        services.AddScoped<UploadAttachmentCommandHandler>();
        services.AddScoped<DownloadAttachmentQueryHandler>();
        services.AddScoped<PurgeAttachmentCommandHandler>();
    }
}
