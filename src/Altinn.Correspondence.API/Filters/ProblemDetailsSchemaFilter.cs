using Altinn.Authorization.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Filters;

public class ProblemDetailsSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (!typeof(ProblemDetails).IsAssignableFrom(context.Type))
            return;

        if (schema is OpenApiSchema concreteSchema)
            concreteSchema.AdditionalPropertiesAllowed = false;

        var props = schema.Properties ?? new Dictionary<string, IOpenApiSchema>();

        if (props.ContainsKey("code"))
            props["code"] = StringSchema();

        props["traceId"] = StringNullableSchema("OpenTelemetry trace ID for the request.");

        if (typeof(AltinnValidationProblemDetails).IsAssignableFrom(context.Type))
        {
            props["code"] = StringNullableSchema("Altinn error code (e.g. STD-00000).");

            props["validationErrors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array | JsonSchemaType.Null,
                Items = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["code"] = StringSchema("Altinn validation error code (e.g. CORR.VLD-00000)."),
                        ["detail"] = StringSchema("Human-readable description of the validation error."),
                        ["paths"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Array,
                            Items = StringSchema(),
                            Description = "JSON pointer paths to the fields that failed validation."
                        }
                    }
                },
                Description = "Structured validation errors per field."
            };

            props["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object | JsonSchemaType.Null,
                AdditionalPropertiesAllowed = true,
                AdditionalProperties = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = StringSchema()
                },
                Description = "Field-keyed validation error messages (legacy format)."
            };
        }
        else
        {
            props["errorCode"] = StringNullableSchema("Altinn error code (e.g. CORR-00001).");
        }
    }

    private static OpenApiSchema StringSchema(string? description = null) => new()
    {
        Type = JsonSchemaType.String,
        Description = description
    };

    private static OpenApiSchema StringNullableSchema(string? description = null) => new()
    {
        Type = JsonSchemaType.String | JsonSchemaType.Null,
        Description = description
    };
}
