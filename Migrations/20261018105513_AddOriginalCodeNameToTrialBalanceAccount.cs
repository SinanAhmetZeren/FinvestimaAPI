using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalCodeNameToTrialBalanceAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalCode",
                table: "TrialBalanceAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalName",
                table: "TrialBalanceAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalCode",
                table: "TrialBalanceAccounts");

            migrationBuilder.DropColumn(
                name: "OriginalName",
                table: "TrialBalanceAccounts");
        }
    }
}
