using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkerId",
                table: "Tokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceTaskExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TaskName = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Implementation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetClientId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ResultVariable = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExecutedByClientId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceTaskExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceTaskExecutions_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TaskName = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedBy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: false),
                    Variables = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workers_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Workers_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTaskExecutions_CreatedAt",
                table: "ServiceTaskExecutions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTaskExecutions_ProcessId",
                table: "ServiceTaskExecutions",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTaskExecutions_Status",
                table: "ServiceTaskExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceTaskExecutions_Status_TargetClientId",
                table: "ServiceTaskExecutions",
                columns: new[] { "Status", "TargetClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_CreatedAt",
                table: "Workers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_ProcessId",
                table: "Workers",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_Status",
                table: "Workers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_Status_Type",
                table: "Workers",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TokenId",
                table: "Workers",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workers_Type",
                table: "Workers",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceTaskExecutions");

            migrationBuilder.DropTable(
                name: "Workers");

            migrationBuilder.DropColumn(
                name: "WorkerId",
                table: "Tokens");
        }
    }
}
