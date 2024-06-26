using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsMessageBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntendedPresentation",
                table: "CorrespondenceAttachments");

            migrationBuilder.DropColumn(
                name: "IntendedPresentation",
                table: "Attachments");

            migrationBuilder.AddColumn<bool>(
                name: "IsMessageBody",
                table: "CorrespondenceAttachments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMessageBody",
                table: "CorrespondenceAttachments");

            migrationBuilder.AddColumn<int>(
                name: "IntendedPresentation",
                table: "CorrespondenceAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IntendedPresentation",
                table: "Attachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
