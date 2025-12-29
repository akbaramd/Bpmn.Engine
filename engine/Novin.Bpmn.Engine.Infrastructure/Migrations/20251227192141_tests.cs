using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class tests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessKey",
                table: "Processes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAt",
                table: "Processes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Processes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "Processes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TerminatedAt",
                table: "Processes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "Processes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessKey",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "TerminatedAt",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "Processes");
        }
    }
}
