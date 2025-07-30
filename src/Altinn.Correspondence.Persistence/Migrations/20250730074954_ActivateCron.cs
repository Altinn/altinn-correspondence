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
                BEGIN
                    BEGIN
                        -- Try to create the extension
                        CREATE EXTENSION pg_cron;
                        
                        -- If successful, schedule the weekly ANALYZE job
                        PERFORM cron.schedule(
                          'weekly_analyze',
                          '0 4 * * 0',
                          $$ ANALYZE; $$
                        );
                    EXCEPTION
                        WHEN OTHERS THEN
                            -- Log the error but don't fail the migration
                            RAISE NOTICE 'pg_cron could not be activated: %', SQLERRM;
                    END;
                END
                $do$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    BEGIN
                        -- Try to unschedule the job
                        PERFORM cron.unschedule('weekly_analyze');
                    EXCEPTION
                        WHEN OTHERS THEN
                            -- Log the error but don't fail the migration
                            RAISE NOTICE 'pg_cron unschedule failed: %', SQLERRM;
                    END;
                END
                $do$;
            ");
        }
    }
}
