using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Checkout",
                table: "SagaInstances",
                type: "TEXT",
                nullable: false,
                defaultValue: "{\"quoteId\":\"00000000-0000-0000-0000-000000000000\",\"tender\":{\"cashAmount\":{\"amount\":0,\"currency\":\"USD\"},\"creditsApplied\":0,\"creditValue\":{\"amount\":0,\"currency\":\"USD\"}},\"stayDate\":\"2026-01-01\",\"floorAboveNet\":5}");

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemberId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tender = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Drift = table.Column<string>(type: "TEXT", nullable: true),
                    SupplierReference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropColumn(
                name: "Checkout",
                table: "SagaInstances");
        }
    }
}
