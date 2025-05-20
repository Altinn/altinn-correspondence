using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueIndexAltinn2CorrespondenceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                type: "text",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Altinn2CorrespondenceId",
                schema: "correspondence",
                table: "Correspondences",
                column: "Altinn2CorrespondenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Correspondences_Altinn2CorrespondenceId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.AlterColumn<int>(
                name: "Altinn2AttachmentId",
                schema: "correspondence",
                table: "Attachments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
