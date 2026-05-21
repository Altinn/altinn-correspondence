using Altinn.Correspondence.API.Filters;
using Microsoft.OpenApi;
using System.Reflection;

namespace Altinn.Correspondence.API.Swagger;

internal static class DependencyInjection
{
    public static IServiceCollection AddCorrespondenceOpenApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var registry = CorrespondenceOpenApiDocumentSetup.BuildRegistry(configuration, hostEnvironment);
        services.AddSingleton(registry);

        services.AddSwaggerGen(options =>
        {
            foreach (var descriptor in registry.Descriptors)
            {
                options.SwaggerDoc(descriptor.Name, descriptor.Info);
            }

            options.DocInclusionPredicate((documentName, apiDesc) =>
            {
                if (string.Equals(
                        documentName,
                        CorrespondenceOpenApiConstants.DocumentName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return !OpenApiDocumentationHelper.IsExcludedFromPublicOpenApi(apiDesc);
                }

                return true;
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
