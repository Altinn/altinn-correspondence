using Altinn.Correspondence.API.Filters;
using Altinn.Correspondence.Core.Options;
using Microsoft.OpenApi;
using System.Reflection;

namespace Altinn.Correspondence.API.Swagger;

internal static class DependencyInjection
{
    public static IServiceCollection AddCorrespondenceOpenApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var generalSettings = new GeneralSettings();
        configuration.GetSection(nameof(GeneralSettings)).Bind(generalSettings);

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(CorrespondenceOpenApiConstants.DocumentName, new OpenApiInfo
            {
                Title = CorrespondenceOpenApiConstants.Title,
                Version = CorrespondenceOpenApiConstants.DocumentName,
                Description = CorrespondenceOpenApiConstants.Description
            });

            options.AddSecurityDefinition(CorrespondenceOpenApiConstants.SecuritySchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = CorrespondenceOpenApiConstants.BearerDescription,
                In = ParameterLocation.Header
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.SchemaFilter<ProblemDetailsSchemaFilter>();
            options.SchemaFilter<CorrespondenceSchemaFilter>();
            options.OperationFilter<BadRequestOneOfOperationFilter>();
            options.OperationFilter<CorrespondenceAuthorizeOperationFilter>();
            options.DocumentFilter<CorrespondenceServerDocumentFilter>();
        });

        return services;
    }

    public static WebApplication UseCorrespondenceOpenApi(this WebApplication app)
    {
        var routeTemplate = $"{CorrespondenceOpenApiConstants.RoutePrefix}/{{documentName}}/swagger.json";

        app.UseSwagger(options =>
        {
            options.RouteTemplate = routeTemplate;
        });

        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = CorrespondenceOpenApiConstants.RoutePrefix;
            options.SwaggerEndpoint(
                $"/{CorrespondenceOpenApiConstants.RoutePrefix}/{CorrespondenceOpenApiConstants.DocumentName}/swagger.json",
                $"{CorrespondenceOpenApiConstants.Title} {CorrespondenceOpenApiConstants.DocumentName}");
            options.DocumentTitle = CorrespondenceOpenApiConstants.Title;
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
            options.EnableTryItOutByDefault();
        });

        return app;
    }
}
