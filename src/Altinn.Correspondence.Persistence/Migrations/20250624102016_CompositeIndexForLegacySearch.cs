using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompositeIndexForLegacySearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_Correspondences_Recipient_RequestedPublishTime_Id""
                ON correspondence.""Correspondences""
                USING btree (""Recipient"", ""RequestedPublishTime"" DESC, ""Id"");
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Correspondences_Recipient_RequestedPublishTime_Id"";
            ");
        }
    }
}
