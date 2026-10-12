using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddValuationTaskIdToTrialBalanceAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ValuationTaskId",
                table: "TrialBalanceAccounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE ""TrialBalanceAccounts"" a
                SET ""ValuationTaskId"" = y.""ValuationTaskId""
                FROM ""TrialBalanceYears"" y
                WHERE a.""TrialBalanceYearId"" = y.""Id"";
            ");

            migrationBuilder.CreateIndex(
                name: "IX_TrialBalanceAccounts_ValuationTaskId",
                table: "TrialBalanceAccounts",
                column: "ValuationTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrialBalanceAccounts_ValuationTasks_ValuationTaskId",
                table: "TrialBalanceAccounts",
                column: "ValuationTaskId",
                principalTable: "ValuationTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrialBalanceAccounts_ValuationTasks_ValuationTaskId",
                table: "TrialBalanceAccounts");

            migrationBuilder.DropIndex(
                name: "IX_TrialBalanceAccounts_ValuationTaskId",
                table: "TrialBalanceAccounts");

            migrationBuilder.DropColumn(
                name: "ValuationTaskId",
                table: "TrialBalanceAccounts");
        }
    }
}
