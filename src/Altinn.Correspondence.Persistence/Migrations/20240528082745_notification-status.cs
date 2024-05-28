using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class notificationstatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "Created",
                table: "CorrespondenceNotifications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateTable(
                name: "CorrespondenceNotificationStatusEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusText = table.Column<string>(type: "text", nullable: true),
                    StatusChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceNotificationStatusEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceNotificationStatusEntity_CorrespondenceNotifi~",
                        column: x => x.NotificationId,
                        principalTable: "CorrespondenceNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotificationStatusEntity_NotificationId",
                table: "CorrespondenceNotificationStatusEntity",
                column: "NotificationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceNotificationStatusEntity");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "CorrespondenceNotifications");
        }
    }
}
