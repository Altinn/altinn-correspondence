using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialize : Migration
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
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsEncrypted = table.Column<bool>(type: "boolean", nullable: false),
                    Checksum = table.Column<string>(type: "text", nullable: true),
                    SendersReference = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    DataType = table.Column<string>(type: "text", nullable: false),
                    IntendedPresentation = table.Column<int>(type: "integer", nullable: false),
                    RestrictionName = table.Column<string>(type: "text", nullable: false),
                    ExpirationTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DataLocationUrl = table.Column<string>(type: "text", nullable: true),
                    DataLocationType = table.Column<int>(type: "integer", nullable: false)
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
                    VisibleFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AllowSystemDeleteAfter = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PropertyList = table.Column<Dictionary<string, string>>(type: "hstore", maxLength: 10, nullable: false),
                    IsReservable = table.Column<bool>(type: "boolean", nullable: true)
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
                    Language = table.Column<int>(type: "integer", nullable: false),
                    MessageTitle = table.Column<string>(type: "text", nullable: false),
                    MessageSummary = table.Column<string>(type: "text", nullable: false),
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
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    LinkText = table.Column<string>(type: "text", nullable: false),
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
                name: "AttachmentEntityCorrespondenceContentEntity",
                columns: table => new
                {
                    AttachmentsId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrespondenceContentsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentEntityCorrespondenceContentEntity", x => new { x.AttachmentsId, x.CorrespondenceContentsId });
                    table.ForeignKey(
                        name: "FK_AttachmentEntityCorrespondenceContentEntity_Attachments_Att~",
                        column: x => x.AttachmentsId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttachmentEntityCorrespondenceContentEntity_CorrespondenceC~",
                        column: x => x.CorrespondenceContentsId,
                        principalTable: "CorrespondenceContents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentEntityCorrespondenceContentEntity_CorrespondenceC~",
                table: "AttachmentEntityCorrespondenceContentEntity",
                column: "CorrespondenceContentsId");

            migrationBuilder.CreateIndex(
                name: "IX_AttachmentStatuses_AttachmentId",
                table: "AttachmentStatuses",
                column: "AttachmentId");

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
                name: "AttachmentEntityCorrespondenceContentEntity");

            migrationBuilder.DropTable(
                name: "AttachmentStatuses");

            migrationBuilder.DropTable(
                name: "CorrespondenceNotifications");

            migrationBuilder.DropTable(
                name: "CorrespondenceReplyOptions");

            migrationBuilder.DropTable(
                name: "CorrespondenceStatuses");

            migrationBuilder.DropTable(
                name: "ExternalReferences");

            migrationBuilder.DropTable(
                name: "CorrespondenceContents");

            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "Correspondences");
        }
    }
}
