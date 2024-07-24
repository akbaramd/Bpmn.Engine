using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Dashbaord.Migrations
{
    /// <inheritdoc />
    public partial class Init6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "Processes");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Assignee",
                table: "Tasks",
                column: "Assignee");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CandidateByGroups",
                table: "Tasks",
                column: "CandidateByGroups");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CandidateByUsers",
                table: "Tasks",
                column: "CandidateByUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TaskId",
                table: "Tasks",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_Assignee",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_CandidateByGroups",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_CandidateByUsers",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TaskId",
                table: "Tasks");

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessId",
                table: "Processes",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
