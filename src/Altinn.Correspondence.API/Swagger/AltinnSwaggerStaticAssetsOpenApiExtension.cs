using Microsoft.OpenApi;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class AltinnSwaggerStaticAssetsOpenApiExtension(
    string catchAllPath,
    IReadOnlyList<string> knownAssets,
    string applicationRoutePrefix) : IOpenApiExtension
{
    public void Write(IOpenApiWriter writer, OpenApiSpecVersion specVersion)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("catchAllPath");
        writer.WriteValue(catchAllPath);
        writer.WritePropertyName("applicationRoutePrefix");
        writer.WriteValue(applicationRoutePrefix);
        writer.WritePropertyName("knownAssets");
        writer.WriteStartArray();
        foreach (var asset in knownAssets)
        {
            writer.WriteValue(asset);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
