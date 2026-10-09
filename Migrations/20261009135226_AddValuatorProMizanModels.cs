using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinvestimaAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddValuatorProMizanModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Firmalar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ad = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firmalar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MizanExtractionJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MizanExtractionJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MizanExtractionJobs_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MizanYillar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirmaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Months = table.Column<int>(type: "integer", nullable: false),
                    IsPartial = table.Column<bool>(type: "boolean", nullable: false),
                    FileDebitTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    FileCreditTotal = table.Column<decimal>(type: "numeric", nullable: true),
                    Label = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MizanYillar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MizanYillar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MizanHesaplar",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MizanYilId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Debit = table.Column<decimal>(type: "numeric", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric", nullable: false),
                    DebitBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    CreditBalance = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MizanHesaplar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MizanHesaplar_MizanYillar_MizanYilId",
                        column: x => x.MizanYilId,
                        principalTable: "MizanYillar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MizanExtractionJobs_FirmaId",
                table: "MizanExtractionJobs",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_MizanHesaplar_MizanYilId_Code",
                table: "MizanHesaplar",
                columns: new[] { "MizanYilId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MizanYillar_FirmaId_Year",
                table: "MizanYillar",
                columns: new[] { "FirmaId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MizanExtractionJobs");

            migrationBuilder.DropTable(
                name: "MizanHesaplar");

            migrationBuilder.DropTable(
                name: "MizanYillar");

            migrationBuilder.DropTable(
                name: "Firmalar");
        }
    }
}
