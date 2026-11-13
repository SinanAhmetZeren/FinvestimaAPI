using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class ConvertAllowedEmailToLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AllowedEmails_NormalizedEmail",
                table: "AllowedEmails");

            migrationBuilder.DropColumn(
                name: "Used",
                table: "AllowedEmails");

            migrationBuilder.DropColumn(
                name: "UsedAt",
                table: "AllowedEmails");

            migrationBuilder.CreateIndex(
                name: "IX_AllowedEmails_NormalizedEmail",
                table: "AllowedEmails",
                column: "NormalizedEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AllowedEmails_NormalizedEmail",
                table: "AllowedEmails");

            migrationBuilder.AddColumn<bool>(
                name: "Used",
                table: "AllowedEmails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UsedAt",
                table: "AllowedEmails",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllowedEmails_NormalizedEmail",
                table: "AllowedEmails",
                column: "NormalizedEmail",
                unique: true);
        }
    }
}
