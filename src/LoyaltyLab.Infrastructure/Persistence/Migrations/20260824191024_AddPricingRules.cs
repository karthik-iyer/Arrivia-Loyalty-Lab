using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Markup = table.Column<decimal>(type: "TEXT", nullable: true),
                    Cap = table.Column<decimal>(type: "TEXT", nullable: true),
                    CampaignCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Adjustment = table.Column<decimal>(type: "TEXT", nullable: true),
                    FloorAboveNet = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingRules_PartnerId_Kind_EffectiveFrom_EffectiveTo",
                table: "PricingRules",
                columns: new[] { "PartnerId", "Kind", "EffectiveFrom", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingRules");
        }
    }
}
