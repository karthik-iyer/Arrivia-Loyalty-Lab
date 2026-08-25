using System;
using LoyaltyLab.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyLab.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LoyaltyLabDbContext))]
    [Migration("20260825223000_AddOpportunity")]
    public partial class AddOpportunity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusyPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Start = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    End = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusyPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nudges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WindowStart = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    WindowEnd = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Score = table.Column<decimal>(type: "TEXT", nullable: false),
                    Signals = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SuppressedBecause = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nudges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PriceWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaselineNetRate = table.Column<string>(type: "TEXT", nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceWatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusyPeriods_PartnerId_MemberId_Start",
                table: "BusyPeriods",
                columns: new[] { "PartnerId", "MemberId", "Start" });

            migrationBuilder.CreateIndex(
                name: "IX_Nudges_PartnerId_MemberId_CreatedAt",
                table: "Nudges",
                columns: new[] { "PartnerId", "MemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceWatches_LastCheckedAt",
                table: "PriceWatches",
                column: "LastCheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PriceWatches_PartnerId_OfferId",
                table: "PriceWatches",
                columns: new[] { "PartnerId", "OfferId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BusyPeriods");
            migrationBuilder.DropTable(name: "Nudges");
            migrationBuilder.DropTable(name: "PriceWatches");
        }
    }
}
