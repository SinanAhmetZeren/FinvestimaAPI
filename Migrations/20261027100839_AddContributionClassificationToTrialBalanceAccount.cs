using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionClassificationToTrialBalanceAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContributionClass",
                table: "TrialBalanceAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ContributionClassIsManual",
                table: "TrialBalanceAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributionClass",
                table: "TrialBalanceAccounts");

            migrationBuilder.DropColumn(
                name: "ContributionClassIsManual",
                table: "TrialBalanceAccounts");
        }
    }
}
