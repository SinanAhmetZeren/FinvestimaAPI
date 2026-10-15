using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlySalesObjectAndRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonthlySalesObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    IsRemoved = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySalesObjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySalesObjects_ValuationTasks_ValuationTaskId",
                        column: x => x.ValuationTaskId,
                        principalTable: "ValuationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlySalesRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlySalesObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Revenue = table.Column<decimal>(type: "numeric", nullable: false),
                    MaterialCost = table.Column<decimal>(type: "numeric", nullable: false),
                    Opex = table.Column<decimal>(type: "numeric", nullable: false),
                    IsManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlySalesRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonthlySalesRows_MonthlySalesObjects_MonthlySalesObjectId",
                        column: x => x.MonthlySalesObjectId,
                        principalTable: "MonthlySalesObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySalesObjects_ValuationTaskId",
                table: "MonthlySalesObjects",
                column: "ValuationTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlySalesRows_MonthlySalesObjectId_Year_Month",
                table: "MonthlySalesRows",
                columns: new[] { "MonthlySalesObjectId", "Year", "Month" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonthlySalesRows");

            migrationBuilder.DropTable(
                name: "MonthlySalesObjects");
        }
    }
}
