using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidentialRecipientPublishTimeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Partial index for GetUnopenedConfidentialCorrespondencesForParty
            migrationBuilder.Sql(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS \"IX_Correspondences_Confidential_Recipient_PublishTime\" " +
                "ON correspondence.\"Correspondences\" (\"Recipient\", \"RequestedPublishTime\") " +
                "WHERE \"IsConfidential\" = true;",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP INDEX CONCURRENTLY IF EXISTS correspondence.\"IX_Correspondences_Confidential_Recipient_PublishTime\";",
                suppressTransaction: true);
        }
    }
}
