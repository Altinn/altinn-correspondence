using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAttachmentExpirationTimeWithExpirationInDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExpirationInDays",
                schema: "correspondence",
                table: "Attachments",
                type: "integer",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "Attachments");

            // This column has historically not been used and has ended up as '-infinity' for existing rows.
            // To avoid carrying that sentinel value forward, we reset the column by dropping and re-adding it as nullable.
            // New correspondences will set ExpirationTime explicitly based on max(now, RequestedPublishTime) + ExpirationInDays.
            migrationBuilder.DropColumn(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "CorrespondenceAttachments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "CorrespondenceAttachments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "CorrespondenceAttachments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "CorrespondenceAttachments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "TIMESTAMPTZ '-infinity'");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpirationTime",
                schema: "correspondence",
                table: "Attachments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "ExpirationInDays",
                schema: "correspondence",
                table: "Attachments");
        }
    }
}

