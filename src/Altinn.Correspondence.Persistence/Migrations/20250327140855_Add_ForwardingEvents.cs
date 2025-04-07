using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_ForwardingEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrespondenceForwardingEvents",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ForwardedOnDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ForwardedByPartyUuid = table.Column<Guid>(type: "uuid", nullable: false),
                    ForwardedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ForwardedByUserUuid = table.Column<Guid>(type: "uuid", nullable: false),
                    ForwardedToUserId = table.Column<int>(type: "integer", nullable: true),
                    ForwardedToUserUuid = table.Column<Guid>(type: "uuid", nullable: true),
                    ForwardingText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ForwardedToEmailAddress = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MailboxSupplier = table.Column<string>(type: "text", nullable: true),
                    CorrespondenceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceForwardingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceForwardingEvents_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalSchema: "correspondence",
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceForwardingEvents_CorrespondenceId",
                schema: "correspondence",
                table: "CorrespondenceForwardingEvents",
                column: "CorrespondenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceForwardingEvents",
                schema: "correspondence");
        }
    }
}
