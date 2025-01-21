using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegacyPartyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyParties",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyParties", x => x.Id);
                    table.UniqueConstraint("UK_LegacyParties_PartyId", x => x.PartyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyParties_PartyId",
                schema: "correspondence",
                table: "LegacyParties",
                column: "PartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyParties",
                schema: "correspondence");
        }
    }
}
