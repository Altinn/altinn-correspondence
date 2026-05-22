using Microsoft.OpenApi;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class AltinnCorsOpenApiExtension(CorrespondenceCorsMetadata metadata) : IOpenApiExtension
{
    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        writer.WriteStartObject();
        WriteStringArray(writer, "allowedOrigins", metadata.AllowedOrigins);
        WriteStringArray(writer, "allowedMethods", metadata.AllowedMethods);
        WriteStringArray(writer, "allowedHeaders", metadata.AllowedHeaders);
        writer.WritePropertyName("allowCredentials");
        writer.WriteValue(metadata.AllowCredentials);
        writer.WriteEndObject();
    }

    private static void WriteStringArray(IOpenApiWriter writer, string propertyName, IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteValue(value);
        }

        writer.WriteEndArray();
    }
}

internal static class CorrespondenceCorsOpenApiExtensions
{
    public static void ApplyDocumentExtension(OpenApiDocument swaggerDoc, CorrespondenceCorsMetadata metadata)
    {
        swaggerDoc.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        swaggerDoc.Extensions[CorrespondenceCorsOrigins.OpenApiExtensionName] = new AltinnCorsOpenApiExtension(metadata);
    }

    public static void ApplyOperationExtension(OpenApiOperation operation)
    {
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions[CorrespondenceCorsOrigins.OperationExtensionName] = new OpenApiLiteralExtension(true);
    }
}

internal sealed class OpenApiLiteralExtension(bool value) : IOpenApiExtension
{
    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion) => writer.WriteValue(value);
}
