using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowForwardingOnceWithAndWithoutAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_IncludeAttachments_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                columns: new[] { "CorrespondenceId", "ForwardedToEmailAddress", "IncludeAttachments" },
                unique: true,
                filter: "\"ForwardedToEmailAddress\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_IncludeAttachments_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId_ForwardedToEmailAddress_Unique",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                columns: new[] { "CorrespondenceId", "ForwardedToEmailAddress" },
                unique: true,
                filter: "\"ForwardedToEmailAddress\" IS NOT NULL");
        }
    }
}
