using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Tokens_TokenId",
                table: "Workers");

            migrationBuilder.DropTable(
                name: "ServiceTaskExecutions");

            migrationBuilder.DropTable(
                name: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Workers_Status_Type",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_TokenId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "Workers");

            migrationBuilder.RenameColumn(
                name: "StartedAt",
                table: "Workers",
                newName: "StartedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Workers",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "Workers",
                newName: "CompletedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_Workers_CreatedAt",
                table: "Workers",
                newName: "IX_Workers_CreatedAtUtc");

            // Clean orphan rows BEFORE SQLite rebuilds table (ef_temp_Workers copy)
            migrationBuilder.Sql(@"
DELETE FROM Workers
WHERE ProcessId NOT IN (SELECT Id FROM Processes);

DELETE FROM Workers
WHERE TokenId NOT IN (SELECT Id FROM Tokens);
");
            
            migrationBuilder.AlterColumn<string>(
                name: "Variables",
                table: "Workers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "Workers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                table: "Workers",
                type: "TEXT",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAtUtc",
                table: "Workers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Workers",
                type: "BLOB",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workers_ActorId",
                table: "Workers",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_CompletedAtUtc",
                table: "Workers",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Workers_ProcessId_ElementId",
                table: "Workers",
                columns: new[] { "ProcessId", "ElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_ProcessId_Status_Type",
                table: "Workers",
                columns: new[] { "ProcessId", "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Workers_TokenId",
                table: "Workers",
                column: "TokenId");

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Tokens_TokenId",
                table: "Workers",
                column: "TokenId",
                principalTable: "Tokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workers_Tokens_TokenId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_ActorId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_CompletedAtUtc",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_ProcessId_ElementId",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_ProcessId_Status_Type",
                table: "Workers");

            migrationBuilder.DropIndex(
                name: "IX_Workers_TokenId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "ClaimedAtUtc",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Workers");

            migrationBuilder.RenameColumn(
                name: "StartedAtUtc",
                table: "Workers",
                newName: "StartedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Workers",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CompletedAtUtc",
                table: "Workers",
                newName: "CompletedAt");

            migrationBuilder.RenameIndex(
                name: "IX_Workers_CreatedAtUtc",
                table: "Workers",
                newName: "IX_Workers_CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Variables",
                table: "Workers",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "Workers",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                table: "Workers",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ServiceTaskExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ExecutedByClientId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Implementation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    ResultVariable = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    TargetClientId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TaskName = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false)
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
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssignedTo = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    InputVariables = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OutputVariables = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                });

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

            migrationBuilder.AddForeignKey(
                name: "FK_Workers_Tokens_TokenId",
                table: "Workers",
                column: "TokenId",
                principalTable: "Tokens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
