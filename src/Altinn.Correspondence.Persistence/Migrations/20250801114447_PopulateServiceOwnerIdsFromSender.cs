using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PopulateServiceOwnerIdsFromSender : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update Correspondences table: Extract organization number from Sender field (everything after the last ':')
            // Only set ServiceOwnerId if the extracted organization number exists in ServiceOwners table
            migrationBuilder.Sql(@"
                UPDATE correspondence.""Correspondences"" 
                SET ""ServiceOwnerId"" = SPLIT_PART(""Sender"", ':', -1)
                WHERE ""Sender"" IS NOT NULL 
                AND ""Sender"" LIKE '%:%'
                AND ""ServiceOwnerId"" IS NULL
                AND EXISTS (
                    SELECT 1 FROM correspondence.""ServiceOwners"" 
                    WHERE ""Id"" = SPLIT_PART(""Correspondences"".""Sender"", ':', -1)
                );
            ");

            // Update Attachments table: Extract organization number from Sender field (everything after the last ':')
            // Only set ServiceOwnerId if the extracted organization number exists in ServiceOwners table
            migrationBuilder.Sql(@"
                UPDATE correspondence.""Attachments"" 
                SET ""ServiceOwnerId"" = SPLIT_PART(""Sender"", ':', -1)
                WHERE ""Sender"" IS NOT NULL 
                AND ""Sender"" LIKE '%:%'
                AND ""ServiceOwnerId"" IS NULL
                AND EXISTS (
                    SELECT 1 FROM correspondence.""ServiceOwners"" 
                    WHERE ""Id"" = SPLIT_PART(""Attachments"".""Sender"", ':', -1)
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clear ServiceOwnerIds in Correspondences table
            migrationBuilder.Sql(@"
                UPDATE correspondence.""Correspondences"" 
                SET ""ServiceOwnerId"" = NULL;
            ");

            // Clear ServiceOwnerIds in Attachments table
            migrationBuilder.Sql(@"
                UPDATE correspondence.""Attachments"" 
                SET ""ServiceOwnerId"" = NULL;
            ");
        }
    }
}
