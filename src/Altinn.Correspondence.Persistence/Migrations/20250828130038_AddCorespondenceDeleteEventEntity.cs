using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorespondenceDeleteEventEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrespondenceDeleteEvents",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    EventOccurred = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyUuid = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncedFromAltinn2 = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceDeleteEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceDeleteEvents_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalSchema: "correspondence",
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceDeleteEvents_CorrespondenceId",
                schema: "correspondence",
                table: "CorrespondenceDeleteEvents",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceDeleteEvents_EventType",
                schema: "correspondence",
                table: "CorrespondenceDeleteEvents",
                column: "EventType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceDeleteEvents",
                schema: "correspondence");
        }
    }
}
