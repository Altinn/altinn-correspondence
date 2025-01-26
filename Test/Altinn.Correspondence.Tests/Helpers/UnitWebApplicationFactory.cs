using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Correspondence.Tests.Helpers
{
    internal class UnitWebApplicationFactory : CustomWebApplicationFactory
    {
        public UnitWebApplicationFactory(Action<IServiceCollection> customServices)
        {
            CustomServices = customServices;
        }
    }
}
