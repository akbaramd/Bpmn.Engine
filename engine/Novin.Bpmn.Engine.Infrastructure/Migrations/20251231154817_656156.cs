using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _656156 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParentTokenIds",
                table: "Tokens");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTokenId",
                table: "Tokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ParentTokenId",
                table: "Tokens",
                column: "ParentTokenId",
                filter: "[ParentTokenId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tokens_ParentTokenId",
                table: "Tokens");

            migrationBuilder.DropColumn(
                name: "ParentTokenId",
                table: "Tokens");

            migrationBuilder.AddColumn<string>(
                name: "ParentTokenIds",
                table: "Tokens",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
