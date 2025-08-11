using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallPgStatStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: pg_stat_statements extension requires manual installation
            // Run: CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Run: DROP EXTENSION IF EXISTS pg_stat_statements;
        }
    }
}
