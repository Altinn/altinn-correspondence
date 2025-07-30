using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateCron : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(@"
                    DO $do$
                    DECLARE
                        extension_available BOOLEAN;
                    BEGIN
                        -- Check if pg_cron extension is available in the system
                        SELECT EXISTS (
                            SELECT 1 FROM pg_available_extensions 
                            WHERE name = 'pg_cron'
                        ) INTO extension_available;
                        
                        -- Only create and schedule if pg_cron is available
                        IF extension_available THEN
                            CREATE EXTENSION IF NOT EXISTS pg_cron;
                            
                            -- Schedule weekly ANALYZE job
                            PERFORM cron.schedule(
                              'weekly_analyze',
                              '0 4 * * 0',
                              $$ ANALYZE; $$
                            );
                        END IF;
                    END
                    $do$;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(@"
                    DO $do$
                    DECLARE
                        extension_available BOOLEAN;
                    BEGIN
                        -- Check if pg_cron extension is available in the system
                        SELECT EXISTS (
                            SELECT 1 FROM pg_available_extensions 
                            WHERE name = 'pg_cron'
                        ) INTO extension_available;
                        
                        -- Only unschedule if pg_cron is available
                        IF extension_available THEN
                            PERFORM cron.unschedule('weekly_analyze');
                        END IF;
                    END
                    $do$;
                ");
            }
        }
    }
}
