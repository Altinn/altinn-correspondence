using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Update.Internal;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotficationMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("Delete from correspondence.\"CorrespondenceNotificationStatuses\"");
            migrationBuilder.Sql("Update correspondence.\"CorrespondenceNotifications\" SET \"CustomTextToken\" = null");
            migrationBuilder.Sql("Update correspondence.\"CorrespondenceNotifications\" SET \"NotificationTemplate\" = null");

            migrationBuilder.Sql("ALTER TABLE correspondence.\"CorrespondenceNotifications\" ALTER COLUMN \"NotificationTemplate\" TYPE integer USING \"NotificationTemplate\"::integer;");

            migrationBuilder.DropTable(
                name: "CorrespondenceNotificationStatuses",
                schema: "correspondence");

            migrationBuilder.DropColumn(
                name: "CustomTextToken",
                schema: "correspondence",
                table: "CorrespondenceNotifications");

            migrationBuilder.RenameColumn(
                name: "SendersReference",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                newName: "NotificationAddress");



            migrationBuilder.AddColumn<int>(
                name: "NotificationChannel",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "NotificationOrderId",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NotificationSent",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationChannel",
                schema: "correspondence",
                table: "CorrespondenceNotifications");

            migrationBuilder.DropColumn(
                name: "NotificationOrderId",
                schema: "correspondence",
                table: "CorrespondenceNotifications");

            migrationBuilder.DropColumn(
                name: "NotificationSent",
                schema: "correspondence",
                table: "CorrespondenceNotifications");

            migrationBuilder.RenameColumn(
                name: "NotificationAddress",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                newName: "SendersReference");

            migrationBuilder.AlterColumn<string>(
                name: "NotificationTemplate",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "CustomTextToken",
                schema: "correspondence",
                table: "CorrespondenceNotifications",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorrespondenceNotificationStatuses",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StatusText = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceNotificationStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceNotificationStatuses_CorrespondenceNotificati~",
                        column: x => x.NotificationId,
                        principalSchema: "correspondence",
                        principalTable: "CorrespondenceNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotificationStatuses_NotificationId",
                schema: "correspondence",
                table: "CorrespondenceNotificationStatuses",
                column: "NotificationId");
        }
    }
}
