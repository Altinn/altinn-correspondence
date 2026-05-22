using Microsoft.AspNetCore.Cors;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class CorrespondenceCorsOperationFilter : IOperationFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiOperation operation, OperationFilterContext context)
    {
        var hasCors = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<EnableCorsAttribute>()
            .Any(attribute => attribute.PolicyName == Common.Constants.AuthorizationConstants.ArbeidsflateCors);

        if (!hasCors)
        {
            return;
        }

        CorrespondenceCorsOpenApiExtensions.ApplyOperationExtension(operation);
    }
}
