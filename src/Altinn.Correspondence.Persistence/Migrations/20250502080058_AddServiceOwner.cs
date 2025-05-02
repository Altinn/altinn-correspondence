using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceOwners",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageProviders",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StorageResourceName = table.Column<string>(type: "text", nullable: false),
                    ServiceOwnerId = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceOwnerEntityId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageProviders_ServiceOwners_ServiceOwnerEntityId",
                        column: x => x.ServiceOwnerEntityId,
                        principalSchema: "correspondence",
                        principalTable: "ServiceOwners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageProviders",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "ServiceOwners",
                schema: "correspondence");
        }
    }
}
