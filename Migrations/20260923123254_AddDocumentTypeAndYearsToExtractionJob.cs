using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeAndYearsToExtractionJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "FinancialExtractionJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Years",
                table: "FinancialExtractionJobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "FinancialExtractionJobs");

            migrationBuilder.DropColumn(
                name: "Years",
                table: "FinancialExtractionJobs");
        }
    }
}
