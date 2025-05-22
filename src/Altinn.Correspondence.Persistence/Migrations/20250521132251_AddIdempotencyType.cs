using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyKeys_Correspondences_CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys");

            migrationBuilder.AlterColumn<int>(
                name: "StatusAction",
                schema: "correspondence",
                table: "IdempotencyKeys",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "IdempotencyType",
                schema: "correspondence",
                table: "IdempotencyKeys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyKeys_Correspondences_CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                column: "CorrespondenceId",
                principalSchema: "correspondence",
                principalTable: "Correspondences",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IdempotencyKeys_Correspondences_CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys");

            migrationBuilder.DropColumn(
                name: "IdempotencyType",
                schema: "correspondence",
                table: "IdempotencyKeys");

            migrationBuilder.AlterColumn<int>(
                name: "StatusAction",
                schema: "correspondence",
                table: "IdempotencyKeys",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IdempotencyKeys_Correspondences_CorrespondenceId",
                schema: "correspondence",
                table: "IdempotencyKeys",
                column: "CorrespondenceId",
                principalSchema: "correspondence",
                principalTable: "Correspondences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
