using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Recipient_RequestedPublishTime_Id",
                schema: "correspondence",
                table: "Correspondences",
                columns: new[] { "Recipient", "RequestedPublishTime", "Id" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Correspondences_Recipient_RequestedPublishTime_Id",
                schema: "correspondence",
                table: "Correspondences");
        }
    }
}
