namespace Altinn.Correspondence.Tests.Invariants
{
    public class InitializeServiceOwnerSqlJobNamespaceTests
    {
        // In the database migration that creates the job for InitializeServiceOwner, the namespace is hardcoded as "Altinn.Correspondence.Application.InitializeServiceOwner.InitializeServiceOwnerHandler"
        // This test ensures that the namespace in the code is consistent with the one in the database migration.
        // If the namespace in the code changes, this test will fail, and you will need to update the migration in the database accordingly.
        // This test is important because it ensures that the job created in the database is correctly associated with the handler in the code.
        // Migration: 20250502080058_AddServiceOwner.cs
        [Fact]
        public void InitializeServiceOwner_HasNamespaceConsistentWithSqlJobForInitializeServiceOwner()
        {
            Assert.Equal("Altinn.Correspondence.Application.InitializeServiceOwner.InitializeServiceOwnerHandler", typeof(Altinn.Correspondence.Application.InitializeServiceOwner.InitializeServiceOwnerHandler).FullName);
        }
    }
}
