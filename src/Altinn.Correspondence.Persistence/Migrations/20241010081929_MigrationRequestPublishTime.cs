using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrationRequestPublishTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VisibleFrom",
                schema: "correspondence",
                table: "Correspondences",
                newName: "RequestedPublishTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequestedPublishTime",
                schema: "correspondence",
                table: "Correspondences",
                newName: "VisibleFrom");
        }
    }
}
