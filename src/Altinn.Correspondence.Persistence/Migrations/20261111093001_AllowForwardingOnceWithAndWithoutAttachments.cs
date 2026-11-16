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
            // The old index only allows one forwarding event per email address. Rows that differ only by IncludeAttachments
            // are forwarding history and are not deleted, so the downgrade is stopped until they are resolved manually.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM correspondence."CorrespondenceForwardingEvents"
                        WHERE "ForwardedToEmailAddress" IS NOT NULL
                        GROUP BY "CorrespondenceId", "ForwardedToEmailAddress"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot revert AllowForwardingOnceWithAndWithoutAttachments: some correspondences have been forwarded to the same email address both with and without attachments. Resolve these forwarding events manually before reverting.';
                    END IF;
                END $$;
                """);

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
