using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Update_Notication_Template_LanguageCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "correspondence",
                table: "NotificationTemplates",
                keyColumn: "Language",
                keyValue: "no",
                column: "Language",
                value: "nb"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "correspondence",
                table: "NotificationTemplates",
                keyColumn: "Language",
                keyValue: "nb",
                column: "Language",
                value: "no"
            );
        }
    }
}
