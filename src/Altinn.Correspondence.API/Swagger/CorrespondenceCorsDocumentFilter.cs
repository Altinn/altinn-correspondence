using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class CorrespondenceCorsDocumentFilter(
    IOptions<AltinnOptions> altinnOptions,
    IOptions<GeneralSettings> generalSettings) : IDocumentFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var metadata = CorrespondenceCorsOrigins.CreateMetadata(altinnOptions.Value, generalSettings.Value);
        CorrespondenceCorsOpenApiExtensions.ApplyDocumentExtension(swaggerDoc, metadata);

        var originList = string.Join(", ", metadata.AllowedOrigins);
        swaggerDoc.Info ??= new Microsoft.OpenApi.OpenApiInfo();
        swaggerDoc.Info.Description = string.IsNullOrWhiteSpace(swaggerDoc.Info.Description)
            ? BuildCorsDescription(originList)
            : swaggerDoc.Info.Description + "\n\n" + BuildCorsDescription(originList);
    }

    private static string BuildCorsDescription(string originList) =>
        "Cross-origin browser access (Arbeidsflate and hosted Swagger UI) is limited to: " + originList
        + ". Operations marked with extension `" + CorrespondenceCorsOrigins.OperationExtensionName
        + "` require these CORS settings at the API gateway.";
}
