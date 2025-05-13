using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Altinn2AttachmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Attachments_Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                column: "Altinn2AttachmentId");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Correspondences_Altinn2CorrespondenceId",
                schema: "correspondence",
                table: "Correspondences",
                column: "Altinn2CorrespondenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments");
        }
    }
}
