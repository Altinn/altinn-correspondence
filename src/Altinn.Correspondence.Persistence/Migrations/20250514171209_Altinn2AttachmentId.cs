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
            migrationBuilder.AddColumn<string>(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                column: "Altinn2AttachmentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attachments_Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments");
        }
    }
}
