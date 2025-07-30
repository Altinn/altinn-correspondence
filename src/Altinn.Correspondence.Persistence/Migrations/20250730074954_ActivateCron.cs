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
                    -- Try to create the extension
                    EXECUTE 'CREATE EXTENSION IF NOT EXISTS pg_cron';
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_cron extension creation failed: %', SQLERRM;
                END
                $do$;
            ");
            
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    -- Schedule the weekly ANALYZE job
                    PERFORM cron.schedule(
                      'weekly_analyze',
                      '0 4 * * 0',
                      $$ ANALYZE; $$
                    );
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_cron scheduling failed: %', SQLERRM;
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
                    -- Try to unschedule the job first
                    PERFORM cron.unschedule('weekly_analyze');
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_cron unschedule failed: %', SQLERRM;
                END
                $do$;
            ");
            
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    -- Try to remove the extension
                    EXECUTE 'DROP EXTENSION IF EXISTS pg_cron';
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_cron extension removal failed: %', SQLERRM;
                END
                $do$;
            ");
        }
    }
}
