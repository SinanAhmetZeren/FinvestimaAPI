using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddLanguageToComparableMultiplesJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ComparableMultiplesJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "ComparableMultiplesJobs");
        }
    }
}
