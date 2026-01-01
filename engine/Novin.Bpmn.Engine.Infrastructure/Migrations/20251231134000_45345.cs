using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _45345 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedViaFlowId",
                table: "Tokens");

            migrationBuilder.AddColumn<string>(
                name: "ArrivedViaFlowIds",
                table: "Tokens",
                type: "text",
                nullable: false,
                defaultValue: "");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedViaFlowIds",
                table: "Tokens");


            migrationBuilder.AddColumn<string>(
                name: "ArrivedViaFlowId",
                table: "Tokens",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

        
        }
    }
}
