using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndexIsolatedToA3Correspondences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX CONCURRENTLY IX_Correspondences_Recipient_RequestedPublishTime_A3Only 
                ON correspondence.""Correspondences"" (""Recipient"", ""RequestedPublishTime"" DESC, ""Id"")
                WHERE ""Altinn2CorrespondenceId"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql(@"
                DROP INDEX CONCURRENTLY IX_Correspondences_Recipient_RequestedPublishTime_A3Only;
            ");
        }
    }
}
