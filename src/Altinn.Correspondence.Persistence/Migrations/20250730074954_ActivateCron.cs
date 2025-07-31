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
                    CREATE EXTENSION pg_cron;
                    
                    PERFORM cron.schedule(
                      'weekly_analyze',
                      '0 4 * * 0',
                      $$ ANALYZE; $$
                    );
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
                    PERFORM cron.unschedule('weekly_analyze');
                    
                    DROP EXTENSION pg_cron;
                END
                $do$;
            ");
        }
    }
}
