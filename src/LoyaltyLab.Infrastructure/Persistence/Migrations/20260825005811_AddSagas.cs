using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoyaltyLab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSagas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SagaInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BookingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CurrentStepIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaSteps",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SagaInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExternalReference = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Compensation = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaSteps", x => new { x.SagaInstanceId, x.Kind });
                    table.ForeignKey(
                        name: "FK_SagaSteps_SagaInstances_SagaInstanceId",
                        column: x => x.SagaInstanceId,
                        principalTable: "SagaInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaInstances_BookingId",
                table: "SagaInstances",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SagaInstances_Status_LastHeartbeatAt",
                table: "SagaInstances",
                columns: new[] { "Status", "LastHeartbeatAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaSteps");

            migrationBuilder.DropTable(
                name: "SagaInstances");
        }
    }
}
