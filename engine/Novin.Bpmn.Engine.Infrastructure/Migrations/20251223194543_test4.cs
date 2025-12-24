using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class test4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "_history",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "_nextNodes",
                table: "Tokens");

            migrationBuilder.RenameColumn(
                name: "_parentNodeIds",
                table: "Tokens",
                newName: "ScopeId");

            migrationBuilder.AddColumn<string>(
                name: "ArrivedViaFlowId",
                table: "Tokens",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExecutable",
                table: "Tokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Variables",
                table: "Tokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "_parentTokenIds",
                table: "Tokens",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedViaFlowId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "IsExecutable",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "Variables",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "_parentTokenIds",
                table: "Tokens");

            migrationBuilder.RenameColumn(
                name: "ScopeId",
                table: "Tokens",
                newName: "_parentNodeIds");

            migrationBuilder.AddColumn<string>(
                name: "_history",
                table: "Tokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "_nextNodes",
                table: "Tokens",
                type: "TEXT",
                nullable: true);
        }
    }
}
