using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Helpers
{
    public static class ApplicationDbContextExtensions
    {
        public static ApplicationDbContext MigrateWithLock(this ApplicationDbContext dbContext)
        {
            try
            {
                using var connection = dbContext.Database.GetDbConnection();
                connection.Open();

                var lockId = 123456789;
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT pg_advisory_lock({lockId});";
                command.ExecuteNonQuery(); // This will wait until lock is available

                try
                {
                    if (dbContext.Database.GetPendingMigrations().Any())
                    {
                        dbContext.Database.Migrate();
                    }
                }
                finally
                {
                    command.CommandText = $"SELECT pg_advisory_unlock({lockId});";
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration failed: {ex.Message}");
            }

            return dbContext;
        }
    }
}
