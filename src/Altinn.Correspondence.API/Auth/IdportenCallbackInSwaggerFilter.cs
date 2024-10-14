using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Auth;

public class IdportenCallbackInSwaggerFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument openApiDocument, DocumentFilterContext context)
    {
        var operation = new OpenApiOperation
        {
            Summary = "Callback for Idporten login"
        };
        operation.Tags.Add(new OpenApiTag { Name = "ID-Porten" });
        var response = new OpenApiResponse
        {
            Description = "Success"
        };
        response.Content.Add("application/json", new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                Type = "object",
                AdditionalPropertiesAllowed = true
            }
        });
        operation.Responses.Add("200", response);
        var pathItem = new OpenApiPathItem();
        pathItem.AddOperation(OperationType.Get, operation);
        openApiDocument?.Paths.Add("/idporten-callback", pathItem);
    }
}