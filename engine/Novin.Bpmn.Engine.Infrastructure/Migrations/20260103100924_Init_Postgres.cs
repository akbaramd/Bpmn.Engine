using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Novin.Bpmn.Engine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init_Postgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoundaryEventSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    HostElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BoundaryElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsInterrupting = table.Column<bool>(type: "boolean", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExternalJobKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CorrelationKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ActivityInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TimerExpression = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NextDueAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FireCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Meta = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoundaryEventSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BpmnXml = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeployedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    BpmnHash = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionFlowRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ToElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ViaFlowIds = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionFlowRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Cause = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastOccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Occurrences = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TaskName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Implementation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LockId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NodeInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsExecutable = table.Column<bool>(type: "boolean", nullable: false),
                    ArrivedViaFlowIds = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MessageName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MessageType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LockId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartitionKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessBpmnId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BusinessKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TerminatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TerminationReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    NodeInstanceIds = table.Column<string>(type: "text", nullable: false),
                    TokenIds = table.Column<string>(type: "text", nullable: false),
                    VariablesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivityInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArrivedViaFlowIds = table.Column<string>(type: "text", nullable: false),
                    ScopeStack = table.Column<string>(type: "text", nullable: false),
                    Variables = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ElementId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TaskName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompletedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: false),
                    Variables = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_DueAt",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "Kind", "State", "DueAt" },
                filter: "\"Kind\" = 'Timer' AND \"State\" = 'Active' AND \"DueAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_Kind_State_NextDueAtUtc",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "Kind", "State", "NextDueAtUtc" },
                filter: "\"Kind\" = 'Timer' AND \"State\" = 'Active' AND \"NextDueAtUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_NodeInstanceId",
                table: "BoundaryEventSubscriptions",
                column: "NodeInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_ProcessId",
                table: "BoundaryEventSubscriptions",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_ProcessId_ActivityInstanceId",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "ProcessId", "ActivityInstanceId" },
                filter: "\"ActivityInstanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_ProcessId_TokenScopeId",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "ProcessId", "TokenScopeId" },
                filter: "\"TokenScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_State_DueAt",
                table: "BoundaryEventSubscriptions",
                columns: new[] { "State", "DueAt" },
                filter: "\"DueAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BoundaryEventSubscriptions_TokenId",
                table: "BoundaryEventSubscriptions",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionFlow_Process_OccurredAtUtc",
                table: "ExecutionFlowRecords",
                columns: new[] { "ProcessId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionFlow_Process_Position",
                table: "ExecutionFlowRecords",
                columns: new[] { "ProcessId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionFlow_Process_Scope_Position",
                table: "ExecutionFlowRecords",
                columns: new[] { "ProcessId", "ScopeId", "Position" },
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionFlow_Process_ToElement",
                table: "ExecutionFlowRecords",
                columns: new[] { "ProcessId", "ToElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionFlow_Token_Position",
                table: "ExecutionFlowRecords",
                columns: new[] { "TokenId", "Position" });

            migrationBuilder.CreateIndex(
                name: "UX_ExecutionFlow_EventKey",
                table: "ExecutionFlowRecords",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_NodeInstanceId",
                table: "Incidents",
                column: "NodeInstanceId",
                filter: "\"NodeInstanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ProcessId",
                table: "Incidents",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_ProcessId_Status_LastOccurredAtUtc",
                table: "Incidents",
                columns: new[] { "ProcessId", "Status", "LastOccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_TokenId",
                table: "Incidents",
                column: "TokenId",
                filter: "\"TokenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Incidents_WorkerId",
                table: "Incidents",
                column: "WorkerId",
                filter: "\"WorkerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ClientId",
                table: "Jobs",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CompletedAtUtc",
                table: "Jobs",
                column: "CompletedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreatedAtUtc",
                table: "Jobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_NodeInstanceId",
                table: "Jobs",
                column: "NodeInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProcessId",
                table: "Jobs",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProcessId_Status",
                table: "Jobs",
                columns: new[] { "ProcessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_LockedUntilUtc",
                table: "Jobs",
                columns: new[] { "Status", "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_NextAttemptAtUtc",
                table: "Jobs",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_TokenId",
                table: "Jobs",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_ActivityInstance",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "ActivityInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_Element",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "ElementId" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_Element_Created",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "ElementId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_Scope",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "ScopeId" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_State",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_Process_State_Created",
                table: "NodeInstances",
                columns: new[] { "ProcessId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_TokenId",
                table: "NodeInstances",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_TokenId_State",
                table: "NodeInstances",
                columns: new[] { "TokenId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_NodeInstance_WorkerId_State",
                table: "NodeInstances",
                columns: new[] { "WorkerId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_AggregateId",
                table: "OutboxMessages",
                column: "AggregateId",
                filter: "\"AggregateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CorrelationId",
                table: "OutboxMessages",
                column: "CorrelationId",
                filter: "\"CorrelationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PartitionKey_Status_Occurred",
                table: "OutboxMessages",
                columns: new[] { "PartitionKey", "Status", "OccurredOnUtc" },
                filter: "\"PartitionKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LockedUntil",
                table: "OutboxMessages",
                columns: new[] { "Status", "LockedUntilUtc" },
                filter: "\"Status\" = 1 AND \"LockedUntilUtc\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_Occurred_NextAttempt",
                table: "OutboxMessages",
                columns: new[] { "Status", "OccurredOnUtc", "NextAttemptOnUtc" },
                filter: "\"Status\" IN (0, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DeploymentId",
                table: "Processes",
                column: "DeploymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DeploymentId_ProcessBpmnId",
                table: "Processes",
                columns: new[] { "DeploymentId", "ProcessBpmnId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ProjectId",
                table: "Processes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ProjectId_BusinessKey",
                table: "Processes",
                columns: new[] { "ProjectId", "BusinessKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ProjectId_CreatedAtUtc",
                table: "Processes",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ProjectId_State",
                table: "Processes",
                columns: new[] { "ProjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Token_ParentTokenId",
                table: "Tokens",
                column: "ParentTokenId",
                filter: "\"ParentTokenId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Token_Process_ActivityInstance",
                table: "Tokens",
                columns: new[] { "ProcessId", "ActivityInstanceId" },
                filter: "\"ActivityInstanceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Token_Process_Element_State",
                table: "Tokens",
                columns: new[] { "ProcessId", "CurrentElementId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Token_Process_Scope",
                table: "Tokens",
                columns: new[] { "ProcessId", "ScopeId" },
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Token_Process_State",
                table: "Tokens",
                columns: new[] { "ProcessId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Token_Scope_Element_State",
                table: "Tokens",
                columns: new[] { "ScopeId", "CurrentElementId", "State" },
                filter: "\"ScopeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_ClaimedByUserId",
                table: "UserTasks",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_CompletedByUserId",
                table: "UserTasks",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_NodeInstanceId",
                table: "UserTasks",
                column: "NodeInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_ProcessId",
                table: "UserTasks",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_ProcessId_Status_CreatedAtUtc",
                table: "UserTasks",
                columns: new[] { "ProcessId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_Status_CreatedAtUtc",
                table: "UserTasks",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTasks_TokenId",
                table: "UserTasks",
                column: "TokenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoundaryEventSubscriptions");

            migrationBuilder.DropTable(
                name: "Deployments");

            migrationBuilder.DropTable(
                name: "ExecutionFlowRecords");

            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "NodeInstances");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "Processes");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Tokens");

            migrationBuilder.DropTable(
                name: "UserTasks");
        }
    }
}
