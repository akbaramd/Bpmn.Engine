using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProcessToUseDeploymentIdAndProcessBpmnId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProcessDefinitionId",
                table: "Processes",
                newName: "ProcessBpmnId");

            migrationBuilder.AddColumn<Guid>(
                name: "DeploymentId",
                table: "Processes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeploymentId",
                table: "Processes");

            migrationBuilder.RenameColumn(
                name: "ProcessBpmnId",
                table: "Processes",
                newName: "ProcessDefinitionId");
        }
    }
}
