using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class UserTaskService : IUserTaskService
{
    private readonly IWorkerRepository _workerRepository;
    private readonly IUnitOfWork _uow;

    public UserTaskService(IWorkerRepository workerRepository, IUnitOfWork uow)
    {
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task<Guid> CreateAsync(Process process, Token token, BpmnUserTask ut, CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (ut is null) throw new ArgumentNullException(nameof(ut));

        if (string.IsNullOrWhiteSpace(ut.id))
            throw new InvalidOperationException("UserTask BPMN id is null/empty.");

        // bypass token should never create tasks
        if (!token.IsExecutable)
        {
            token.Wait("Bypass token reached UserTask (ignored).");
            return Guid.Empty;
        }

        // Idempotency: اگر worker برای این token وجود دارد دوباره نساز
        // (تو repo شما می‌تواند GetByTokenIdAsync داشته باشد)
        var existing = await _workerRepository.GetByTokenIdAsync(token.Id);
        if (existing is not null)
        {
            // اگر قبلاً ساخته شده و هنوز تمام نشده => دوباره Wait
            if (existing.Status is WorkerStatus.Pending or WorkerStatus.InProgress)
            {
                token.Wait($"Waiting for user task completion: {ut.name ?? ut.id}", existing.Id);
                return Guid.Empty;
            }

            // اگر Completed/Failed/TimedOut شده، بسته به سیاست:
            // اینجا فرض می‌کنیم دوباره نسازیم و ادامه را handlerِ resume مدیریت کند.
            // (یا می‌توانی یک worker جدید بسازی)
        }

        var meta = BonyanUserTaskExtensions.ExtractUserTaskMetadata(ut);

        var splits = meta?.FormKey?.Split("@");
        var formKey = (splits?.Length>1)?splits?[1]:splits?[0];
        var clientId = (splits?.Length>1)?splits?[0]:"";
        // ساخت worker: از یک Factory مشترک استفاده کن (ترجیحاً Worker.Create(...) واحد)
        var worker = Job.CreateUserTask(
            processId: process.Id,
            tokenId: token.Id,
            elementId: ut.id,
            taskName: ut.name ?? "User Task",
            new UserTaskSpec(meta.FormKey,formKey,"1.0","",TargetClientId:clientId,Assignee:meta.Assignee,CandidateGroups:meta.CandidateGroups,CandidateUsers:meta.CandidateUsers,Priority:meta.Priority,DueDateUtc:meta.DueDateUtc,Description:meta.Description),
           payloadVariables:token.Variables);

        // فرم و بقیه چیزها را در Metadata ذخیره کن
        worker.SetMeta("bonyan:type", "userTask");
        if (!string.IsNullOrWhiteSpace(meta.FormKey))
            worker.SetMeta("bonyan:formKey", meta.FormKey);

        if (meta.CandidateUsers?.Count > 0)
            worker.SetMeta("bonyan:candidateUsers", string.Join(",", meta.CandidateUsers));

        if (meta.CandidateGroups?.Count > 0)
            worker.SetMeta("bonyan:candidateGroups", string.Join(",", meta.CandidateGroups));

        if (meta.Priority.HasValue)
            worker.SetMeta("bonyan:priority", meta.Priority.Value.ToString(CultureInfo.InvariantCulture));

        if (meta.DueDateUtc.HasValue)
            worker.SetMeta("bonyan:dueDate", meta.DueDateUtc.Value.ToString("O", CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(meta.Description))
            worker.SetMeta("bonyan:description", meta.Description);

        // Variables snapshot (اگر می‌خواهی UI فرم با variables پر شود)
        foreach (var kv in token.Variables)
            worker.SetVariable(kv.Key, kv.Value);

        await _workerRepository.AddAsync(worker, ct);

        return worker.Id;
    }
}

/// <summary>
/// Result of reading bonyan extension elements for a user task.
/// </summary>
internal sealed record BonyanUserTaskMeta(
    string? FormKey,
    string? Assignee,
    List<string>? CandidateUsers,
    List<string>? CandidateGroups,
    int? Priority,
    DateTime? DueDateUtc,
    string? Description);

internal static class BonyanUserTaskExtensions
{
    public static BonyanUserTaskMeta ExtractUserTaskMetadata(BpmnUserTask ut)
    {
        var ext = TryGetExtensionElements(ut);
        var nodes = EnumerateExtensionNodes(ext);

        foreach (var n in nodes)
        {
            if (TryReadBonyanUserTask(n, out var raw))
            {
                return new BonyanUserTaskMeta(
                    FormKey: NullIfEmpty(raw.FormKey),
                    Assignee: NullIfEmpty(raw.Assignee),
                    CandidateUsers: SplitList(raw.CandidateUsers),
                    CandidateGroups: SplitList(raw.CandidateGroups),
                    Priority: TryInt(raw.Priority),
                    DueDateUtc: TryParseDueDate(raw.DueDate),
                    Description: NullIfEmpty(raw.Description)
                );
            }
        }

        // اگر نبود، همش null برگرده
        return new BonyanUserTaskMeta(null, null, null, null, null, null, null);
    }

    // ---------- locate extensionElements (handles different generated shapes) ----------
    private static object? TryGetExtensionElements(BpmnUserTask ut)
    {
        var t = ut.GetType();
        return t.GetProperty("extensionElements")?.GetValue(ut)
            ?? t.GetProperty("ExtensionElements")?.GetValue(ut)
            ?? t.GetProperty("extensionElements1")?.GetValue(ut);
    }

    private static IEnumerable<object> EnumerateExtensionNodes(object? extensionElements)
    {
        if (extensionElements is null) yield break;

        // direct arrays/lists
        if (extensionElements is XmlElement[] xeArr) { foreach (var x in xeArr) yield return x; yield break; }
        if (extensionElements is XElement[] xlArr)   { foreach (var x in xlArr) yield return x; yield break; }

        if (extensionElements is IEnumerable<object> objEnum && extensionElements is not string)
        {
            foreach (var x in objEnum) yield return x!;
            yield break;
        }

        // common XSD-generated properties: Any / Items
        var t = extensionElements.GetType();
        object? any =
            t.GetProperty("Any")?.GetValue(extensionElements)
            ?? t.GetProperty("Items")?.GetValue(extensionElements)
            ?? t.GetProperty("Any1")?.GetValue(extensionElements);

        if (any is null) yield break;

        if (any is XmlElement[] anyXe) { foreach (var x in anyXe) yield return x; yield break; }
        if (any is XElement[] anyXl)   { foreach (var x in anyXl) yield return x; yield break; }

        if (any is IEnumerable<object> anyObj && any is not string)
        {
            foreach (var x in anyObj) yield return x!;
            yield break;
        }

        yield return any;
    }

    // ---------- parse bonyan:userTask ----------
    private sealed record RawUserTask(
        string? FormKey,
        string? Assignee,
        string? CandidateUsers,
        string? CandidateGroups,
        string? Priority,
        string? DueDate,
        string? Description);

    private static bool TryReadBonyanUserTask(object node, out RawUserTask raw)
    {
        raw = default!;

        if (node is XmlElement xe)
        {
            if (!IsBonyanUserTask(xe)) return false;

            raw = new RawUserTask(
                FormKey: Attr(xe, "formKey"),
                Assignee: Attr(xe, "assignee"),
                CandidateUsers: Attr(xe, "candidateUsers"),
                CandidateGroups: Attr(xe, "candidateGroups"),
                Priority: Attr(xe, "priority"),
                DueDate: Attr(xe, "dueDate"),
                Description: Attr(xe, "description")
            );
            return true;
        }

        if (node is XElement xl)
        {
            if (!IsBonyanUserTask(xl)) return false;

            raw = new RawUserTask(
                FormKey: Attr(xl, "formKey"),
                Assignee: Attr(xl, "assignee"),
                CandidateUsers: Attr(xl, "candidateUsers"),
                CandidateGroups: Attr(xl, "candidateGroups"),
                Priority: Attr(xl, "priority"),
                DueDate: Attr(xl, "dueDate"),
                Description: Attr(xl, "description")
            );
            return true;
        }

        return false;
    }

    private static bool IsBonyanUserTask(XmlElement e)
    {
        if (!string.Equals(e.LocalName, "userTask", StringComparison.OrdinalIgnoreCase)) return false;

        // Prefer namespace/prefix checks, but keep a safe fallback
        if (string.Equals(e.Prefix, "bonyan", StringComparison.OrdinalIgnoreCase)) return true;

        if (!string.IsNullOrWhiteSpace(e.NamespaceURI) &&
            e.NamespaceURI.Contains("bonyan", StringComparison.OrdinalIgnoreCase))
            return true;

        return e.Name.StartsWith("bonyan:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBonyanUserTask(XElement e)
    {
        if (!string.Equals(e.Name.LocalName, "userTask", StringComparison.OrdinalIgnoreCase)) return false;

        var ns = e.Name.NamespaceName ?? "";
        if (ns.Contains("bonyan", StringComparison.OrdinalIgnoreCase)) return true;

        // fallback: inside extensionElements we accept localName match
        return true;
    }

    // ---------- helpers ----------
    private static string? Attr(XmlElement e, string name) => e.HasAttribute(name) ? e.GetAttribute(name) : null;
    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    private static List<string>? SplitList(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;

        // supports: "a,b,c" OR "a; b" OR "a b"
        var parts = v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .ToList();

        return parts.Count == 0 ? null : parts;
    }

    private static int? TryInt(string? v)
        => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : null;

    private static DateTime? TryParseDueDate(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;

        if (DateTime.TryParse(v, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt.ToUniversalTime();

        // minimal ISO duration support: P2D / PT4H
        if (TryParseIsoDuration(v.Trim(), out var delta))
            return DateTime.UtcNow.Add(delta);

        return null;
    }

    private static bool TryParseIsoDuration(string input, out TimeSpan delta)
    {
        delta = default;
        if (!input.StartsWith("P", StringComparison.OrdinalIgnoreCase)) return false;

        if (input.EndsWith("D", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(input[1..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var days))
        {
            delta = TimeSpan.FromDays(days);
            return true;
        }

        if (input.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
        {
            var body = input[2..];
            if (body.EndsWith("H", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(body[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            {
                delta = TimeSpan.FromHours(h);
                return true;
            }
        }

        return false;
    }

    private static string? NullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
}

