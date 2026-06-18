using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrespondencesPartyResourcePublishTimeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Composite indexes so the GetCorrespondences query can satisfy the party filter (Recipient/Sender),
            // the ResourceId filter and the "ORDER BY RequestedPublishTime DESC LIMIT" from a single index scan,
            // avoiding the BitmapAnd against the large IX_Correspondences_ResourceId index.
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Correspondences_Recipient_ResourceId_RequestedPublishTime\" " +
                "ON correspondence.\"Correspondences\" (\"Recipient\", \"ResourceId\", \"RequestedPublishTime\" DESC, \"Id\");",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Correspondences_Sender_ResourceId_RequestedPublishTime\" " +
                "ON correspondence.\"Correspondences\" (\"Sender\", \"ResourceId\", \"RequestedPublishTime\" DESC, \"Id\");",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_Correspondences_Recipient_ResourceId_RequestedPublishTime\";",
                suppressTransaction: true);

            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_Correspondences_Sender_ResourceId_RequestedPublishTime\";",
                suppressTransaction: true);
        }
    }
}
