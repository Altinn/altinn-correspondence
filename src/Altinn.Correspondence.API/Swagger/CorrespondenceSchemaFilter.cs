using System.Reflection;
using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Excludes properties marked with <see cref="JsonIgnoreAttribute"/> from the OpenAPI schema.
/// </summary>
internal sealed class CorrespondenceSchemaFilter : ISchemaFilter
{
    public void Apply(Microsoft.OpenApi.IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not Microsoft.OpenApi.OpenApiSchema concreteSchema)
        {
            return;
        }

        var ignoredPropertyNames = context.Type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ignoredPropertyNames.Count == 0 || concreteSchema.Properties is null)
        {
            return;
        }

        foreach (var propertyName in ignoredPropertyNames)
        {
            concreteSchema.Properties.Remove(propertyName);
        }
    }
}
