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
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_cron;");
            
            migrationBuilder.Sql(@"
                SELECT cron.schedule(
                  'weekly_analyze',
                  '0 4 * * 0',
                  $$ ANALYZE; $$
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SELECT cron.unschedule('weekly_analyze');");
        }
    }
}
