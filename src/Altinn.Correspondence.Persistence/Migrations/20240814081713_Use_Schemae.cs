using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Use_Schemae : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "correspondence");

            migrationBuilder.RenameTable(
                name: "ExternalReferences",
                newName: "ExternalReferences",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceStatuses",
                newName: "CorrespondenceStatuses",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "Correspondences",
                newName: "Correspondences",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceReplyOptions",
                newName: "CorrespondenceReplyOptions",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceNotificationStatuses",
                newName: "CorrespondenceNotificationStatuses",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceNotifications",
                newName: "CorrespondenceNotifications",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceContents",
                newName: "CorrespondenceContents",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "CorrespondenceAttachments",
                newName: "CorrespondenceAttachments",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "AttachmentStatuses",
                newName: "AttachmentStatuses",
                newSchema: "correspondence");

            migrationBuilder.RenameTable(
                name: "Attachments",
                newName: "Attachments",
                newSchema: "correspondence");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ExternalReferences",
                schema: "correspondence",
                newName: "ExternalReferences");

            migrationBuilder.RenameTable(
                name: "CorrespondenceStatuses",
                schema: "correspondence",
                newName: "CorrespondenceStatuses");

            migrationBuilder.RenameTable(
                name: "Correspondences",
                schema: "correspondence",
                newName: "Correspondences");

            migrationBuilder.RenameTable(
                name: "CorrespondenceReplyOptions",
                schema: "correspondence",
                newName: "CorrespondenceReplyOptions");

            migrationBuilder.RenameTable(
                name: "CorrespondenceNotificationStatuses",
                schema: "correspondence",
                newName: "CorrespondenceNotificationStatuses");

            migrationBuilder.RenameTable(
                name: "CorrespondenceNotifications",
                schema: "correspondence",
                newName: "CorrespondenceNotifications");

            migrationBuilder.RenameTable(
                name: "CorrespondenceContents",
                schema: "correspondence",
                newName: "CorrespondenceContents");

            migrationBuilder.RenameTable(
                name: "CorrespondenceAttachments",
                schema: "correspondence",
                newName: "CorrespondenceAttachments");

            migrationBuilder.RenameTable(
                name: "AttachmentStatuses",
                schema: "correspondence",
                newName: "AttachmentStatuses");

            migrationBuilder.RenameTable(
                name: "Attachments",
                schema: "correspondence",
                newName: "Attachments");
        }
    }
}
