using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_DB_Setup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:hstore", ",,");

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RestrictionName = table.Column<string>(type: "text", nullable: true),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: true),
                    SendersReference = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Sender = table.Column<string>(type: "text", nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    DataLocationUrl = table.Column<string>(type: "text", nullable: true),
                    DataLocationType = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Recipient = table.Column<string>(type: "text", nullable: false),
                    Sender = table.Column<string>(type: "text", nullable: false),
                    SendersReference = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    MessageSender = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    VisibleFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllowSystemDeleteAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyList = table.Column<Dictionary<string, string>>(type: "hstore", maxLength: 10, nullable: false),
                    IsReservable = table.Column<bool>(type: "boolean", nullable: true),
                    MarkedUnread = table.Column<bool>(type: "boolean", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttachmentStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusText = table.Column<string>(type: "text", nullable: false),
                    StatusChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttachmentStatuses_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    MessageTitle = table.Column<string>(type: "text", nullable: false),
                    MessageSummary = table.Column<string>(type: "text", nullable: false),
                    MessageBody = table.Column<string>(type: "text", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceContents_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationTemplate = table.Column<string>(type: "text", nullable: false),
                    CustomTextToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SendersReference = table.Column<string>(type: "text", nullable: true),
                    RequestedSendTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceNotifications_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceReplyOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkURL = table.Column<string>(type: "text", nullable: false),
                    LinkText = table.Column<string>(type: "text", nullable: true),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceReplyOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReplyOptions_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusText = table.Column<string>(type: "text", nullable: false),
                    StatusChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceStatuses_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceValue = table.Column<string>(type: "text", nullable: false),
                    ReferenceType = table.Column<int>(type: "integer", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalReferences_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpirationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrespondenceContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachments_CorrespondenceContents_Correspond~",
                        column: x => x.CorrespondenceContentId,
                        principalTable: "CorrespondenceContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceNotificationStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusText = table.Column<string>(type: "text", nullable: false),
                    StatusChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceNotificationStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceNotificationStatuses_CorrespondenceNotificati~",
                        column: x => x.NotificationId,
                        principalTable: "CorrespondenceNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentStatuses_AttachmentId",
                table: "AttachmentStatuses",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_AttachmentId",
                table: "CorrespondenceAttachments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_CorrespondenceContentId",
                table: "CorrespondenceAttachments",
                column: "CorrespondenceContentId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceContents_CorrespondenceId",
                table: "CorrespondenceContents",
                column: "CorrespondenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotifications_CorrespondenceId",
                table: "CorrespondenceNotifications",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceNotificationStatuses_NotificationId",
                table: "CorrespondenceNotificationStatuses",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReplyOptions_CorrespondenceId",
                table: "CorrespondenceReplyOptions",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceStatuses_CorrespondenceId",
                table: "CorrespondenceStatuses",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalReferences_CorrespondenceId",
                table: "ExternalReferences",
                column: "CorrespondenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentStatuses");

            migrationBuilder.DropTable(
                name: "CorrespondenceAttachments");

            migrationBuilder.DropTable(
                name: "CorrespondenceNotificationStatuses");

            migrationBuilder.DropTable(
                name: "CorrespondenceReplyOptions");

            migrationBuilder.DropTable(
                name: "CorrespondenceStatuses");

            migrationBuilder.DropTable(
                name: "ExternalReferences");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "CorrespondenceContents");

            migrationBuilder.DropTable(
                name: "CorrespondenceNotifications");

            migrationBuilder.DropTable(
                name: "Correspondences");
        }
    }
}
