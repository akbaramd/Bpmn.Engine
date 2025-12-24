using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class test3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeTokenHistoryEntries");

            migrationBuilder.DropTable(
                name: "ProcessHistories");

            migrationBuilder.DropTable(
                name: "TokenHistoryEntries");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ParentTokenId",
                table: "Tokens");

            migrationBuilder.RenameColumn(
                name: "_tokenIds",
                table: "Processes",
                newName: "TokenIds");

            migrationBuilder.AlterColumn<string>(
                name: "_parentNodeIds",
                table: "Tokens",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "_nextNodes",
                table: "Tokens",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "_history",
                table: "Tokens",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TokenIds",
                table: "Processes",
                newName: "_tokenIds");

            migrationBuilder.AlterColumn<string>(
                name: "_parentNodeIds",
                table: "Tokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "_nextNodes",
                table: "Tokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "_history",
                table: "Tokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentNodeId",
                table: "Tokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTokenId",
                table: "Tokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    FailedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NodeName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Variables = table.Column<string>(type: "TEXT", nullable: false),
                    _childNodeIds = table.Column<string>(type: "TEXT", nullable: false),
                    _currentTokenIds = table.Column<string>(type: "TEXT", nullable: false),
                    _history = table.Column<string>(type: "TEXT", nullable: false),
                    _parentNodeIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessHistories",
                columns: table => new
                {
                    ProcessId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessHistories", x => new { x.ProcessId, x.Id });
                    table.ForeignKey(
                        name: "FK_ProcessHistories_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TokenHistoryEntries",
                columns: table => new
                {
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    LeftAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ReachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    Variables = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenHistoryEntries", x => new { x.TokenId, x.Id });
                    table.ForeignKey(
                        name: "FK_TokenHistoryEntries_Tokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "Tokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NodeTokenHistoryEntries",
                columns: table => new
                {
                    NodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElementId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OutputVariables = table.Column<string>(type: "TEXT", nullable: true),
                    ReachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TokenId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeTokenHistoryEntries", x => new { x.NodeId, x.Id });
                    table.ForeignKey(
                        name: "FK_NodeTokenHistoryEntries_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
