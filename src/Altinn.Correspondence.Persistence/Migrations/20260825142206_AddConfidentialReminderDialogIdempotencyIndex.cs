using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidentialReminderDialogIdempotencyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_PartyUrn_ConfidentialReminderDialog",
                schema: "correspondence",
                table: "IdempotencyKeys",
                column: "PartyUrn",
                unique: true,
                filter: "\"IdempotencyType\" = 7 AND \"PartyUrn\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IdempotencyKeys_PartyUrn_ConfidentialReminderDialog",
                schema: "correspondence",
                table: "IdempotencyKeys");
        }
    }
}
