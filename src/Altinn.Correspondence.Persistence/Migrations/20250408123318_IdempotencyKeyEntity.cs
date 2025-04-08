using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdempotencyKeyEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StatusAction = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalSchema: "correspondence",
                        principalTable: "Attachments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IdempotencyKeys_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalSchema: "correspondence",
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_AttachmentId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                column: "CorrespondenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys",
                schema: "correspondence");
        }
    }
}
