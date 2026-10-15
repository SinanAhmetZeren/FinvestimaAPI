using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class MergeFinancialExtractionJobIntoTrialBalanceExtractionJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinancialExtractionJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ValuationTaskId",
                table: "TrialBalanceExtractionJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "TrialBalanceExtractionJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "TrialBalanceExtractionJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Years",
                table: "TrialBalanceExtractionJobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "TrialBalanceExtractionJobs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TrialBalanceExtractionJobs");

            migrationBuilder.DropColumn(
                name: "Years",
                table: "TrialBalanceExtractionJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ValuationTaskId",
                table: "TrialBalanceExtractionJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "FinancialExtractionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Years = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialExtractionJobs", x => x.Id);
                });
        }
    }
}
