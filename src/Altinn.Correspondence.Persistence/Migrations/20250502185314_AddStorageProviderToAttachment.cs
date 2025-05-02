using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageProviderToAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StorageProviderId",
                schema: "correspondence",
                table: "Attachments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StorageProviderId",
                schema: "correspondence",
                table: "Attachments",
                column: "StorageProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_StorageProviders_StorageProviderId",
                schema: "correspondence",
                table: "Attachments",
                column: "StorageProviderId",
                principalSchema: "correspondence",
                principalTable: "StorageProviders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_StorageProviders_StorageProviderId",
                schema: "correspondence",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_StorageProviderId",
                schema: "correspondence",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "StorageProviderId",
                schema: "correspondence",
                table: "Attachments");
        }
    }
}
