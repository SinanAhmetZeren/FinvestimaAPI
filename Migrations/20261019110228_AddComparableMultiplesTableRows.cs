using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddComparableMultiplesTableRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComparableMultiplesTableRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValuationTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    RangeLow = table.Column<decimal>(type: "numeric", nullable: false),
                    RangeHigh = table.Column<decimal>(type: "numeric", nullable: false),
                    Median = table.Column<decimal>(type: "numeric", nullable: false),
                    Vintage = table.Column<string>(type: "text", nullable: true),
                    SourceBasis = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: true),
                    IsEstimate = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparableMultiplesTableRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComparableMultiplesTableRows_ValuationTasks_ValuationTaskId",
                        column: x => x.ValuationTaskId,
                        principalTable: "ValuationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComparableMultiplesTableRows_ValuationTaskId",
                table: "ComparableMultiplesTableRows",
                column: "ValuationTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComparableMultiplesTableRows");
        }
    }
}
