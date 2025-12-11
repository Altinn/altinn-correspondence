using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseServiceOwnerIdAsFkForStorageProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageProviders_ServiceOwners_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders");

            migrationBuilder.DropIndex(
                name: "IX_StorageProviders_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders");

            migrationBuilder.DropColumn(
                name: "ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders");

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_ServiceOwnerId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageProviders_ServiceOwners_ServiceOwnerId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerId",
                principalSchema: "correspondence",
                principalTable: "ServiceOwners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StorageProviders_ServiceOwners_ServiceOwnerId",
                schema: "correspondence",
                table: "StorageProviders");

            migrationBuilder.DropIndex(
                name: "IX_StorageProviders_ServiceOwnerId",
                schema: "correspondence",
                table: "StorageProviders");

            migrationBuilder.AddColumn<string>(
                name: "ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_StorageProviders_ServiceOwners_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerEntityId",
                principalSchema: "correspondence",
                principalTable: "ServiceOwners",
                principalColumn: "Id");
        }
    }
}
