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
            migrationBuilder.Sql(@"
                DO $do$
                DECLARE
                    max_conns INTEGER;
                BEGIN
                    -- Sjekk max_connections for å bestemme tier
                    SELECT setting::INTEGER INTO max_conns 
                    FROM pg_settings WHERE name = 'max_connections';
                    
                    -- Opprett pg_cron og planlegg jobb kun hvis det er General Purpose eller høyere (max_connections > 100)
                    IF max_conns > 100 THEN
                        CREATE EXTENSION IF NOT EXISTS pg_cron;
                        
                        -- Schedule weekly ANALYZE job
                        PERFORM cron.schedule(
                          'weekly_analyze',
                          '0 4 * * 0',
                          $$ ANALYZE; $$
                        );
                        
                        RAISE NOTICE 'pg_cron extension created and weekly ANALYZE job scheduled (tier supports it)';
                    ELSE
                        RAISE NOTICE 'pg_cron not created - tier likely does not support it (max_connections: %)', max_conns;
                    END IF;
                END
                $do$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                DECLARE
                    max_conns INTEGER;
                BEGIN
                    -- Sjekk max_connections for å bestemme tier
                    SELECT setting::INTEGER INTO max_conns 
                    FROM pg_settings WHERE name = 'max_connections';
                    
                    -- Avbryt jobb og fjern extension kun hvis tier støtter det
                    IF max_conns > 100 THEN
                        -- Try to unschedule the job first
                        PERFORM cron.unschedule('weekly_analyze');
                        
                        -- Then remove the extension
                        DROP EXTENSION IF EXISTS pg_cron;
                        
                        RAISE NOTICE 'pg_cron job unscheduled and extension removed';
                    ELSE
                        RAISE NOTICE 'pg_cron cleanup skipped - tier does not support it (max_connections: %)', max_conns;
                    END IF;
                END
                $do$;
            ");
        }
    }
}
