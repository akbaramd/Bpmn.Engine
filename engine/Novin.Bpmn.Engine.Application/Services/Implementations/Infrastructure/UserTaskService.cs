// File: Novin.Bpmn.Engine.Application/Services/UserTaskService.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Production-ready UserTask creator with concurrency-safe idempotency.
/// Key = (ProcessId, TokenId, NodeInstanceId, ElementId)
///
/// IMPORTANT:
/// - Does NOT change Token/Node state. (handler does Wait/Resume/Complete)
/// - Only creates/returns a persisted UserTaskInstance record.
/// </summary>
public sealed class UserTaskService : IUserTaskService
{
    private readonly IUserTaskInstanceRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UserTaskService> _logger;

    public UserTaskService(
        IUserTaskInstanceRepository repo,
        IUnitOfWork uow,
        ILogger<UserTaskService> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> CreateOrGetAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnUserTask userTask,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (userTask is null) throw new ArgumentNullException(nameof(userTask));

        var elementId = userTask.id ?? node.ElementId;
        if (string.IsNullOrWhiteSpace(elementId))
            return Guid.Empty;

        // 1) Read-first idempotency
        var existing = await _repo.GetByKeyAsync(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId!,
            ct: ct);

        if (existing is not null)
        {
            _logger.LogDebug(
                "[USER-TASK-SVC] Reuse existing. TaskId={TaskId} P={P} T={T} N={N} E={E}",
                existing.Id, process.Id, token.Id, node.Id, elementId);

            return existing.Id;
        }

        // 2) Create new (domain-correct factory)
        var spec = BuildSpec(userTask);

        IReadOnlyDictionary<string, string>? payloadVariables = null;

        var taskEntity = UserTaskInstance.Create(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId!,
            taskName: userTask.name ?? elementId!,
            spec: spec,
            payloadVariables: payloadVariables);

        await _repo.AddAsync(taskEntity, ct);

        // 3) Concurrency-safe persistence: rely on UNIQUE KEY and handle race
        return taskEntity.Id;
    }

    private static UserTaskSpec BuildSpec(BpmnUserTask t)
    {
        var formKey = ReadString(t, "formKey", "FormKey") ?? "default";
        var assignee = ReadString(t, "assignee", "Assignee");
        var candidateUsers = ReadCsvList(t, "candidateUsers", "CandidateUsers");
        var candidateGroups = ReadCsvList(t, "candidateGroups", "CandidateGroups");

        var description = ReadString(t, "description", "Description") ?? t.name;
        var priority = ReadInt(t, "priority", "Priority");
        var dueDateUtc = ReadDateTimeUtc(t, "dueDateUtc", "DueDateUtc");
        var claimMode = ReadEnum(t, "claimMode", "ClaimMode", defaultValue: UserTaskClaimMode.Claim);

        var custom = new Dictionary<string, string>(StringComparer.Ordinal);

        return new UserTaskSpec(
            FormKey: formKey,
            FormVersion: ReadString(t, "formVersion", "FormVersion"),
            UiSchemaRef: ReadString(t, "uiSchemaRef", "UiSchemaRef"),
            DataSchemaRef: ReadString(t, "dataSchemaRef", "DataSchemaRef"),
            Description: description,
            Priority: priority,
            DueDateUtc: dueDateUtc,
            ClaimMode: claimMode,
            Assignee: assignee,
            CandidateUsers: candidateUsers,
            CandidateGroups: candidateGroups,
            VisibilityPolicy: ReadString(t, "visibilityPolicy", "VisibilityPolicy"),
            CustomMetadata: custom
        );
    }

    private static string? ReadString(object obj, params string[] names)
    {
        foreach (var n in names)
        {
            var p = obj.GetType().GetProperty(n);
            if (p is null) continue;
            if (p.GetValue(obj) is string s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        return null;
    }

    private static int? ReadInt(object obj, params string[] names)
    {
        var s = ReadString(obj, names);
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateTime? ReadDateTimeUtc(object obj, params string[] names)
    {
        var s = ReadString(obj, names);
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return null;
    }

    private static UserTaskClaimMode ReadEnum(object obj, string name1, string name2, UserTaskClaimMode defaultValue)
    {
        var s = ReadString(obj, name1, name2);
        if (string.IsNullOrWhiteSpace(s)) return defaultValue;
        return Enum.TryParse<UserTaskClaimMode>(s, ignoreCase: true, out var m) ? m : defaultValue;
    }

    private static IReadOnlyList<string>? ReadCsvList(object obj, params string[] names)
    {
        var s = ReadString(obj, names);
        if (string.IsNullOrWhiteSpace(s)) return null;

        var list = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return list.Count == 0 ? null : list;
    }
}
