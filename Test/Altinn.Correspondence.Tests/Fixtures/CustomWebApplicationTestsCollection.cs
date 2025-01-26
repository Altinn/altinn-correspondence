using Altinn.Correspondence.Tests.Helpers;

namespace Altinn.Correspondence.Tests.Fixtures
{
    [CollectionDefinition(nameof(CustomWebApplicationTestsCollection))]
    public class CustomWebApplicationTestsCollection : ICollectionFixture<CustomWebApplicationFactory>
    {
    }
}
