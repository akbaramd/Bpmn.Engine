// File: Novin.Bpmn.Engine.Application/Services/UserTaskService.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class UserTaskService : IUserTaskService
{
    private readonly IUserTaskInstanceRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<UserTaskService> _logger;
    private readonly IFeelExpressionEvaluator _feel;

    public UserTaskService(
        IUserTaskInstanceRepository repo,
        IUnitOfWork uow,
        ILogger<UserTaskService> logger,
        IFeelExpressionEvaluator feelExpressionEvaluator)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _feel = feelExpressionEvaluator ?? throw new ArgumentNullException(nameof(feelExpressionEvaluator));
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

        var elementId = (userTask.id ?? node.ElementId)?.Trim();
        if (string.IsNullOrWhiteSpace(elementId))
            return Guid.Empty;

        var existing = await _repo.GetByKeyAsync(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId!,
            ct: ct).ConfigureAwait(false);

        if (existing is not null)
        {
            _logger.LogDebug(
                "[USER-TASK-SVC] Reuse existing. TaskId={TaskId} P={P} T={T} N={N} E={E}",
                existing.Id, process.Id, token.Id, node.Id, elementId);

            return existing.Id;
        }

        var feelCtx = BuildFeelContext(token);
        var spec = await BuildSpecAsync(userTask, feelCtx, ct).ConfigureAwait(false);

        var taskEntity = UserTaskInstance.Create(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId!,
            taskName: (userTask.name ?? elementId!)!.Trim(),
            spec: spec,
            payloadVariables: null);

        await _repo.AddAsync(taskEntity, ct).ConfigureAwait(false);

        return taskEntity.Id;
    }

    private async Task<UserTaskSpec> BuildSpecAsync(
        BpmnUserTask t,
        IReadOnlyDictionary<string, object?> feelCtx,
        CancellationToken ct)
    {
        var bonyan = TryGetBonyanUserTaskElement(t);

        var formKey = Attr(bonyan, "formKey") ?? "default";

        var assigneeRaw = Attr(bonyan, "assignee");
        var candidateUsersRaw = Attr(bonyan, "candidateUsers");
        var candidateGroupsRaw = Attr(bonyan, "candidateGroups");
        var candidateRolesRaw = Attr(bonyan, "candidateRoles");

        var assignee = EvalStringMaybeFeel(assigneeRaw, feelCtx);
        var candidateUsers = EvalListMaybeFeel(candidateUsersRaw, feelCtx);
        var candidateGroups = EvalListMaybeFeel(candidateGroupsRaw, feelCtx);
        var candidateRoles = EvalListMaybeFeel(candidateRolesRaw, feelCtx);

        var description = Attr(bonyan, "description") ?? t.name;
        var priority = ParseInt(Attr(bonyan, "priority"));
        var dueDateUtc = ParseDateUtc(Attr(bonyan, "dueDateUtc"));
        var claimMode = ParseClaimMode(Attr(bonyan, "claimMode"), UserTaskClaimMode.Claim);

        var formVersion = Attr(bonyan, "formVersion");
        var uiSchemaRef = Attr(bonyan, "uiSchemaRef");
        var dataSchemaRef = Attr(bonyan, "dataSchemaRef");
        var visibilityPolicy = Attr(bonyan, "visibilityPolicy");

        return await Task.FromResult(new UserTaskSpec(
            FormKey: formKey,
            FormVersion: formVersion,
            UiSchemaRef: uiSchemaRef,
            DataSchemaRef: dataSchemaRef,
            Description: description,
            Priority: priority,
            DueDateUtc: dueDateUtc,
            ClaimMode: claimMode,
            Assignee: assignee,
            CandidateUsers: candidateUsers,
            CandidateGroups: candidateGroups,
            CandidateRoles: candidateRoles,
            VisibilityPolicy: visibilityPolicy,
            CustomMetadata: new Dictionary<string, string>(StringComparer.Ordinal)
        )).ConfigureAwait(false);
    }

    // ------------------------------
    // Read extensionElements and parse XML (no interfaces, no reflection)
    // ------------------------------
    private static XmlElement? TryGetBonyanUserTaskElement(BpmnUserTask t)
    {
        // Assumption: generated BPMN model exposes:
        //   t.extensionElements.Any : XmlElement[]
        var ext = t.extensionElements;
        if (ext is null) return null;

        var any = ext.Any;
        if (any is null || any.Length == 0) return null;

        foreach (var node in any)
        {
            if (node is not XmlElement xe) continue;
            if (!string.Equals(xe.LocalName, "userTask", StringComparison.OrdinalIgnoreCase)) continue;

            if (!string.IsNullOrWhiteSpace(xe.Prefix) &&
                string.Equals(xe.Prefix, "bonyan", StringComparison.OrdinalIgnoreCase))
                return xe;

            if (!string.IsNullOrWhiteSpace(xe.NamespaceURI) &&
                xe.NamespaceURI.Contains("bonyan", StringComparison.OrdinalIgnoreCase))
                return xe;

            // If serializer dropped prefix/ns, still accept <userTask .../> inside extensionElements
            return xe;
        }

        return null;
    }

    private static string? Attr(XmlElement? el, string name)
    {
        if (el is null) return null;
        var v = el.GetAttribute(name);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static int? ParseInt(string? s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static DateTime? ParseDateUtc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTime.TryParse(
                s,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return null;
    }

    private static UserTaskClaimMode ParseClaimMode(string? s, UserTaskClaimMode def)
        => Enum.TryParse<UserTaskClaimMode>(s, true, out var m) ? m : def;

    // ------------------------------
    // FEEL
    // ------------------------------
    private static bool IsFeel(string s)
    {
        s = s.Trim();
        if (s.StartsWith("=", StringComparison.Ordinal)) return true;
        if (s.StartsWith("${", StringComparison.Ordinal) && s.EndsWith("}", StringComparison.Ordinal)) return true;
        if (s.StartsWith("feel:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string NormalizeFeel(string raw)
    {
        var s = raw.Trim();

        if (s.StartsWith("feel:", StringComparison.OrdinalIgnoreCase))
            s = s.Substring("feel:".Length).Trim();

        if (s.StartsWith("${", StringComparison.Ordinal) && s.EndsWith("}", StringComparison.Ordinal) && s.Length >= 3)
        {
            s = s.Substring(2, s.Length - 3).Trim();
            if (s.StartsWith("=", StringComparison.Ordinal))
                s = s.Substring(1).Trim();
        }
        else if (s.StartsWith("=", StringComparison.Ordinal))
        {
            s = s.Substring(1).Trim();
        }

        return s;
    }

    private string? EvalStringMaybeFeel(string? raw, IReadOnlyDictionary<string, object?> ctx)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();
        if (!IsFeel(s)) return s;


        var r = _feel.Evaluate(s, ctx);
        return ToStringValue(r);
    }

    private IReadOnlyList<string>? EvalListMaybeFeel(string? raw, IReadOnlyDictionary<string, object?> ctx)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var s = raw.Trim();
        if (!IsFeel(s)) return ReadCsvOrJsonArray(s);

        var expr = NormalizeFeel(s);
        if (string.IsNullOrWhiteSpace(expr)) return null;

        var r = _feel.Evaluate(expr, ctx);
        return ToStringList(r);
    }

    private static string? ToStringValue(object? v)
    {
        if (v is null) return null;

        if (v is string s) return string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
                return string.IsNullOrWhiteSpace(je.GetString()) ? null : je.GetString()!.Trim();
            return je.ToString();
        }

        if (v is JsonNode jn)
            return jn.ToJsonString();

        return Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim();
    }

    private static IReadOnlyList<string>? ToStringList(object? v)
    {
        if (v is null) return null;

        if (v is string s)
            return ReadCsvOrJsonArray(s);

        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var item in je.EnumerateArray())
                {
                    var x = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                    if (!string.IsNullOrWhiteSpace(x))
                        list.Add(x.Trim());
                }
                return NormalizeList(list);
            }

            if (je.ValueKind == JsonValueKind.String)
                return ReadCsvOrJsonArray(je.GetString());

            return NormalizeList(new[] { je.ToString() });
        }

        if (v is IEnumerable e && v is not IDictionary)
        {
            var list = new List<string>();
            foreach (var item in e)
            {
                var x = ToStringValue(item);
                if (!string.IsNullOrWhiteSpace(x))
                    list.Add(x.Trim());
            }
            return NormalizeList(list);
        }

        var one = ToStringValue(v);
        return string.IsNullOrWhiteSpace(one) ? null : NormalizeList(new[] { one });
    }

    private static IReadOnlyList<string>? ReadCsvOrJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();

        if (LooksLikeJsonArray(s))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<string[]>(s);
                return NormalizeList(arr ?? Array.Empty<string>());
            }
            catch { }
        }

        var list = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        return NormalizeList(list);
    }

    private static IReadOnlyList<string>? NormalizeList(IEnumerable<string> items)
    {
        var list = items
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return list.Count == 0 ? null : list;
    }

    private static bool LooksLikeJsonArray(string s)
    {
        s = s.Trim();
        return s.Length >= 2 && s[0] == '[' && s[^1] == ']';
    }

    // ------------------------------
    // FEEL context: token.Variables is JSON string
    // ------------------------------
    private static IReadOnlyDictionary<string, object?> BuildFeelContext(Token token)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

     
                    foreach (var kv in token.Variables)
                        dict[kv.Key] = ToDotNet(kv.Value);
             

        dict["tokenId"] = token.Id;
        dict["processId"] = token.ProcessId;

        return dict;
    }

    private static object? ToDotNet(JsonNode? n)
    {
        if (n is null) return null;

        if (n is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<long>(out var l)) return l;
            if (v.TryGetValue<decimal>(out var dec)) return dec;
            if (v.TryGetValue<double>(out var dbl)) return dbl;
            return v.ToJsonString();
        }

        return n;
    }
}
