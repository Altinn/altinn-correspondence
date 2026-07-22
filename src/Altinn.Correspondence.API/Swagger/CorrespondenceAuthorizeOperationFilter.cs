using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class CorrespondenceAuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var authorizeData = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>()
            .ToList();

        if (authorizeData.Count == 0)
        {
            return;
        }

        var scopes = ResolveScopes(authorizeData);
        operation.Security ??= [];

        var schemeReference = new OpenApiSecuritySchemeReference(
            CorrespondenceOpenApiConstants.SecuritySchemeId,
            context.Document,
            externalResource: null);

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [schemeReference] = scopes
        });
    }

    private static List<string> ResolveScopes(IReadOnlyList<IAuthorizeData> authorizeData)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var data in authorizeData)
        {
            if (string.IsNullOrEmpty(data.Policy))
            {
                continue;
            }

            switch (data.Policy)
            {
                case AuthorizationConstants.Sender:
                    scopes.Add(AuthorizationConstants.SenderScope);
                    scopes.Add(AuthorizationConstants.ServiceOwnerScope);
                    break;
                case AuthorizationConstants.Recipient:
                    scopes.Add(AuthorizationConstants.RecipientScope);
                    break;
                case AuthorizationConstants.SenderOrRecipient:
                    scopes.Add(AuthorizationConstants.SenderScope);
                    scopes.Add(AuthorizationConstants.RecipientScope);
                    scopes.Add(AuthorizationConstants.ServiceOwnerScope);
                    break;
                case AuthorizationConstants.DownloadAttachmentPolicy:
                    scopes.Add(AuthorizationConstants.RecipientScope);
                    scopes.Add(AuthorizationConstants.PortalEndUserScope);
                    break;
                case AuthorizationConstants.NotificationCheck:
                    scopes.Add(AuthorizationConstants.NotificationCheckScope);
                    break;
                case AuthorizationConstants.Maintenance:
                    scopes.Add(AuthorizationConstants.MaintenanceScope);
                    scopes.Add(AuthorizationConstants.ServiceOwnerScope);
                    break;
            }
        }

        return scopes.Count > 0 ? scopes.OrderBy(s => s).ToList() : [AuthorizationConstants.SenderScope];
    }
}
