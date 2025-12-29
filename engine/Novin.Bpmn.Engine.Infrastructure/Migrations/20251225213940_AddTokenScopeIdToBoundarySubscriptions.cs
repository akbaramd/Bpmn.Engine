using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenScopeIdToBoundarySubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TokenScopeId",
                table: "BoundarySubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessExecutionNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    NodeName = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    NodeType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SequenceOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousNodeId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScopeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArrivedViaFlowId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ActivityInstanceId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessExecutionNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessExecutionNodes_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessExecutionNodes_ProcessId",
                table: "ProcessExecutionNodes",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessExecutionNodes_ProcessId_NodeId",
                table: "ProcessExecutionNodes",
                columns: new[] { "ProcessId", "NodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessExecutionNodes_ProcessId_SequenceOrder",
                table: "ProcessExecutionNodes",
                columns: new[] { "ProcessId", "SequenceOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessExecutionNodes");

            migrationBuilder.DropColumn(
                name: "TokenScopeId",
                table: "BoundarySubscriptions");
        }
    }
}
