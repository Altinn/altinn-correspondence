using Altinn.Authorization.ProblemDetails;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Filters;

public class BadRequestOneOfOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses is null || !operation.Responses.TryGetValue("400", out var response))
            return;

        if (response.Content is null || !response.Content.TryGetValue("application/json", out var mediaType))
            return;

        var domainErrorSchema = context.SchemaGenerator.GenerateSchema(
            typeof(AltinnProblemDetails), context.SchemaRepository);

        var validationErrorSchema = context.SchemaGenerator.GenerateSchema(
            typeof(AltinnValidationProblemDetails), context.SchemaRepository);

        mediaType.Schema = new OpenApiSchema
        {
            OneOf = [domainErrorSchema, validationErrorSchema]
        };
    }
}
