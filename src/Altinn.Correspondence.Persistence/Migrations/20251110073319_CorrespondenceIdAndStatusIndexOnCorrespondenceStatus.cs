using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrespondenceIdAndStatusIndexOnCorrespondenceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_CorrespondenceStatuses_CorrespondenceId_Status\" " +
                "ON correspondence.\"CorrespondenceStatuses\" (\"CorrespondenceId\" ASC, \"Status\" DESC);",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_CorrespondenceStatuses_CorrespondenceId\";",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_CorrespondenceStatuses_CorrespondenceId\" " +
                "ON correspondence.\"CorrespondenceStatuses\" (\"CorrespondenceId\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_CorrespondenceStatuses_CorrespondenceId_Status\";",
                suppressTransaction: true);
        }
    }
}
