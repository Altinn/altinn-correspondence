using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Altinn.Correspondence.API.Swagger;

/// <summary>
/// Overrides <see cref="HideFromPublicApiAttribute"/> so internal endpoints appear in Swagger when running locally.
/// </summary>
internal sealed class ShowInternalApisInDevelopmentApplicationModelConvention(IHostEnvironment environment)
    : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        foreach (var controller in application.Controllers)
        {
            if (HasHideFromPublicApi(controller.Attributes))
            {
                controller.ApiExplorer.IsVisible = true;
            }

            foreach (var action in controller.Actions)
            {
                if (HasHideFromPublicApi(action.Attributes))
                {
                    action.ApiExplorer.IsVisible = true;
                }
            }
        }
    }

    private static bool HasHideFromPublicApi(IReadOnlyList<object> attributes) =>
        attributes.OfType<HideFromPublicApiAttribute>().Any();
}
