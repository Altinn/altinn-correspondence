using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Altinn2CorrespondenceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Altinn2CorrespondenceId",
                schema: "correspondence",
                table: "Correspondences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Altinn2NotificationId",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Altinn2CorrespondenceId",
                schema: "correspondence",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "Altinn2NotificationId",
                schema: "correspondence",
                table: "CorrespondenceNotifications");
        }
    }
}
