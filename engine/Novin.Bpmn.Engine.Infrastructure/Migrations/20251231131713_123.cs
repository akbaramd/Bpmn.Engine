using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedViaFlowId",
                table: "NodeInstances");

            migrationBuilder.AddColumn<string>(
                name: "ArrivedViaFlowIds",
                table: "NodeInstances",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivedViaFlowIds",
                table: "NodeInstances");

            migrationBuilder.AddColumn<string>(
                name: "ArrivedViaFlowId",
                table: "NodeInstances",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }
    }
}
