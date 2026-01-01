using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class _3423423443 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tokens_ProcessId",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_Tokens_ProcessId_CurrentElementId",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstances_ProcessId",
                table: "NodeInstances");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstances_WorkerId",
                table: "NodeInstances");

            migrationBuilder.RenameIndex(
                name: "IX_Tokens_ProcessId_State",
                table: "Tokens",
                newName: "IX_Token_Process_State");

            migrationBuilder.RenameIndex(
                name: "IX_Tokens_ProcessId_ScopeId",
                table: "Tokens",
                newName: "IX_Token_Process_Scope");

            migrationBuilder.RenameIndex(
                name: "IX_Tokens_ProcessId_ActivityInstanceId",
                table: "Tokens",
                newName: "IX_Token_Process_ActivityInstance");

            migrationBuilder.RenameIndex(
                name: "IX_Tokens_ParentTokenId",
                table: "Tokens",
                newName: "IX_Token_ParentTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstances_TokenId",
                table: "NodeInstances",
                newName: "IX_NodeInstance_TokenId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstances_ProcessId_State_CreatedAtUtc",
                table: "NodeInstances",
                newName: "IX_NodeInstance_Process_State_Created");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstances_ProcessId_ScopeId",
                table: "NodeInstances",
                newName: "IX_NodeInstance_Process_Scope");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstances_ProcessId_ElementId",
                table: "NodeInstances",
                newName: "IX_NodeInstance_Process_Element");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstances_ProcessId_ActivityInstanceId",
                table: "NodeInstances",
                newName: "IX_NodeInstance_Process_ActivityInstance");

            migrationBuilder.AddColumn<int>(
                name: "FireCount",
                table: "BoundaryEventSubscriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFiredAtUtc",
                table: "BoundaryEventSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Meta",
                table: "BoundaryEventSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextDueAtUtc",
                table: "BoundaryEventSubscriptions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimerExpression",
                table: "BoundaryEventSubscriptions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimerType",
                table: "BoundaryEventSubscriptions",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Token_Process_Element_State",
                table: "Tokens",
                columns: new[] { "ProcessId", "CurrentElementId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Token_Scope_Element_State",
                table: "Tokens",
                columns: new[] { "ScopeId", "CurrentElementId", "State" },
                filter: "[ScopeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_Element_Created",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "ElementId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_State",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_TokenId_State",
                table: "NodeInstances",
                columns: new[] { "TokenId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_WorkerId_State",
                table: "NodeInstances",
                columns: new[] { "WorkerId", "State" },
                filter: "[WorkerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_DueAt",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "Kind", "State", "DueAt" },
                filter: "[Kind] = 'Timer' AND [State] = 'Active' AND [DueAt] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_NextDueAtUtc",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "Kind", "State", "NextDueAtUtc" },
                filter: "[Kind] = 'Timer' AND [State] = 'Active' AND [NextDueAtUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Token_Process_Element_State",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_Token_Scope_Element_State",
                table: "Tokens");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstance_Process_Element_Created",
                table: "NodeInstances");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstance_Process_State",
                table: "NodeInstances");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstance_TokenId_State",
                table: "NodeInstances");

            migrationBuilder.DropIndex(
                name: "IX_NodeInstance_WorkerId_State",
                table: "NodeInstances");

            migrationBuilder.DropIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_DueAt",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_NextDueAtUtc",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "FireCount",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "LastFiredAtUtc",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "Meta",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "NextDueAtUtc",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "TimerExpression",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "TimerType",
                table: "BoundaryEventSubscriptions");

            migrationBuilder.RenameIndex(
                name: "IX_Token_Process_State",
                table: "Tokens",
                newName: "IX_Tokens_ProcessId_State");

            migrationBuilder.RenameIndex(
                name: "IX_Token_Process_Scope",
                table: "Tokens",
                newName: "IX_Tokens_ProcessId_ScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_Token_Process_ActivityInstance",
                table: "Tokens",
                newName: "IX_Tokens_ProcessId_ActivityInstanceId");

            migrationBuilder.RenameIndex(
                name: "IX_Token_ParentTokenId",
                table: "Tokens",
                newName: "IX_Tokens_ParentTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstance_TokenId",
                table: "NodeInstances",
                newName: "IX_NodeInstances_TokenId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstance_Process_State_Created",
                table: "NodeInstances",
                newName: "IX_NodeInstances_ProcessId_State_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstance_Process_Scope",
                table: "NodeInstances",
                newName: "IX_NodeInstances_ProcessId_ScopeId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstance_Process_Element",
                table: "NodeInstances",
                newName: "IX_NodeInstances_ProcessId_ElementId");

            migrationBuilder.RenameIndex(
                name: "IX_NodeInstance_Process_ActivityInstance",
                table: "NodeInstances",
                newName: "IX_NodeInstances_ProcessId_ActivityInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ProcessId",
                table: "Tokens",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Tokens_ProcessId_CurrentElementId",
                table: "Tokens",
                columns: new[] { "ProcessId", "CurrentElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstances_ProcessId",
                table: "NodeInstances",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstances_WorkerId",
                table: "NodeInstances",
                column: "WorkerId",
                filter: "[WorkerId] IS NOT NULL");
        }
    }
}
