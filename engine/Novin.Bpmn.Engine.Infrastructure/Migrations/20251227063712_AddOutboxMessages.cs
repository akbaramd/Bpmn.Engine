using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MessageName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MessageType = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<byte>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessedOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LockId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PartitionKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AggregateId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_AggregateId",
                table: "OutboxMessages",
                column: "AggregateId",
                filter: "[AggregateId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CorrelationId",
                table: "OutboxMessages",
                column: "CorrelationId",
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PartitionKey_Status_Occurred",
                table: "OutboxMessages",
                columns: new[] { "PartitionKey", "Status", "OccurredOnUtc" },
                filter: "[PartitionKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LockedUntil",
                table: "OutboxMessages",
                columns: new[] { "Status", "LockedUntilUtc" },
                filter: "[Status] = 1 AND [LockedUntilUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttempt_Occurred",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptOnUtc", "OccurredOnUtc" },
                filter: "[Status] IN (0, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
