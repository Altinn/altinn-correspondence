using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Correspondence.API.Swagger;

internal sealed class CorrespondenceServerDocumentFilter(IOptions<GeneralSettings> generalSettings) : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var baseUrl = generalSettings.Value.CorrespondenceBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        swaggerDoc.Servers =
        [
            new OpenApiServer { Url = baseUrl }
        ];
    }
}
