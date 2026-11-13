using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmationFieldsToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfirmationCode",
                table: "Companies",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationCodeExpiry",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationLastSentAt",
                table: "Companies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationSendCount",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmationCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ConfirmationCodeExpiry",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ConfirmationLastSentAt",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ConfirmationSendCount",
                table: "Companies");
        }
    }
}
