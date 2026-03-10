using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrespondencesSendersReferenceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Correspondences_SendersReference\" " +
                "ON correspondence.\"Correspondences\" (\"SendersReference\");",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_Correspondences_SendersReference\";",
                suppressTransaction: true);
        }
    }
}
