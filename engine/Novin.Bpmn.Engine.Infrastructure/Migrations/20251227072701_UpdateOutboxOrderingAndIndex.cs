using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateOutboxOrderingAndIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttempt_Occurred",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_Occurred_NextAttempt",
                table: "OutboxMessages",
                columns: new[] { "Status", "OccurredOnUtc", "NextAttemptOnUtc" },
                filter: "[Status] IN (0, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_Occurred_NextAttempt",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttempt_Occurred",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptOnUtc", "OccurredOnUtc" },
                filter: "[Status] IN (0, 3)");
        }
    }
}
