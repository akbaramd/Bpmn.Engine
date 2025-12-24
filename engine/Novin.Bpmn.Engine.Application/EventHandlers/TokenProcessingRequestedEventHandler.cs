using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenProcessingRequestedEventHandler
    : INotificationHandler<TokenProcessingRequestedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenProcessingRequestedEventHandler> _logger;

    public TokenProcessingRequestedEventHandler(IUnitOfWork uow, ILogger<TokenProcessingRequestedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenProcessingRequestedEvent n, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);

        try
        {
            var process = await _uow.Processes.GetByIdAsync(n.ProcessId, ct);
            if (process == null) return;

            var token = await _uow.Tokens.GetByIdAsync(n.TokenId, ct);
            if (token == null) return;

            // فقط توکن‌های با حالت Active پردازش می‌شوند
            if (token.State != Domain.ValueObjects.TokenState.Active)
            {
                await _uow.CommitTransactionAsync(ct);
                return;
            }

            var deployment = await _uow.Deployments.GetLatestByDeploymentKeyAsync(process.ProcessDefinitionId, ct);
            if (deployment == null)
            {
                _logger.LogError("Deployment for ProcessDefinitionId {ProcessDefinitionId} not found.", process.ProcessDefinitionId);
                await _uow.CommitTransactionAsync(ct);
                return;
            }

            var defs = new BpmnDefinitionsService(deployment.GetDefinitions());
            var bpmnProcessId = defs.GetFirstProcess().id ?? process.ProcessDefinitionId;

            var element = defs.GetElementById(bpmnProcessId, token.CurrentElementId);
            if (element == null)
            {
                token.Fail($"Element '{token.CurrentElementId}' not found.");
                await _uow.CommitTransactionAsync(ct);
                return;
            }

            // ✅ Non-Executable: فقط مسیر را طی می‌کند (هیچ عملیاتی انجام نمی‌شود)
            if (!token.IsExecutable)
            {
                await HandleBypassToken(process, token, defs, bpmnProcessId, element, ct);
                await _uow.CommitTransactionAsync(ct);
                return;
            }

            // ✅ توکن‌های قابل اجرا (Executable): پردازش انجام می‌شود
            await HandleExecutableToken(process, token, defs, bpmnProcessId, element, ct);

            await _uow.CommitTransactionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token processing failed for ProcessId: {ProcessId}, TokenId: {TokenId}", n.ProcessId, n.TokenId);
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }

    // -------------------- EXECUTABLE --------------------

    private async Task HandleExecutableToken(
        Process process,
        Token token,
        BpmnDefinitionsService defs,
        string pid,
        BpmnFlowElement element,
        CancellationToken ct)
    {
        if (element is BpmnEndEvent)
        {
            token.Complete();
            process.RemoveToken(token.Id);
            return;
        }

        if (element is BpmnUserTask ut)
        {
            // ایجاد یک UserTask واقعی و منتظر ماندن
            var userTask = new UserTask(process.Id, ut.name ?? "User Task", ut.id!);
            await _uow.Tasks.AddAsync(userTask, ct);
            token.Wait();
            return;
        }

        if (element is BpmnGateway gateway)
        {
            await HandleGateway(process, token, defs, pid, gateway, ct);
            return;
        }

        // پردازش عادی (task، service task و ...)
        await MoveToSingleOrForkByOutgoing(process, token, defs, pid, ct, executableMode: true);
    }

    // -------------------- GATEWAY --------------------

    private async Task HandleGateway(
        Process process,
        Token token,
        BpmnDefinitionsService defs,
        string pid,
        BpmnGateway gateway,
        CancellationToken ct)
    {
        var incoming = defs.GetIncomingSequenceFlows(pid, token.CurrentElementId);
        var outgoing = defs.GetOutgoingSequenceFlows(pid, token.CurrentElementId);

        var isJoin = incoming.Count > 1;
        var isFork = outgoing.Count > 1;

        // ✅ MERGE/JOIN: باید منتظر ماند تا تمام incoming flowها رسیدند
        if (isJoin && outgoing.Count == 1)
        {
            await HandleMergeBarrier(process, token, defs, pid, ct);
            return;
        }

        // ✅ FORK/SPLIT
        if (isFork)
        {
            if (gateway is BpmnParallelGateway)
            {
                // AND split => همه‌ی توکن‌ها executable باید تکثیر شوند
                await ForkChildren(process, token, outgoing, ct, selectExecutable: _ => true);
                return;
            }

            if (gateway is BpmnExclusiveGateway)
            {
                // XOR split => فقط یکی executable است و بقیه غیر executable
                var chosen = ChooseOneOutgoing(outgoing, token, process);
                await ForkChildren(process, token, outgoing, ct, selectExecutable: f => FlowKey(f) == FlowKey(chosen));
                return;
            }

            if (gateway is BpmnInclusiveGateway)
            {
                // OR split => برخی executable هستند و برخی غیر executable
                var chosen = ChooseOneOutgoing(outgoing, token, process);
                await ForkChildren(process, token, outgoing, ct, selectExecutable: f => FlowKey(f) == FlowKey(chosen));
                return;
            }

            // سایر گیت‌وی‌ها را به‌صورت معمولی fork می‌کنیم
            await ForkChildren(process, token, outgoing, ct, _ => true);
            return;
        }

        // گیت‌وی با یک ورودی و یک خروجی (عبور مستقیم)
        await MoveToSingleOrForkByOutgoing(process, token, defs, pid, ct, executableMode: true);
    }

    // -------------------- BYPASS (NON-EXECUTABLE) --------------------

    private async Task HandleBypassToken(
        Process process,
        Token token,
        BpmnDefinitionsService defs,
        string pid,
        BpmnFlowElement element,
        CancellationToken ct)
    {
        // توکن‌های که غیر قابل اجرا هستند فقط مسیر را طی می‌کنند
        if (element is BpmnGateway)
        {
            var incoming = defs.GetIncomingSequenceFlows(pid, token.CurrentElementId);
            var outgoing = defs.GetOutgoingSequenceFlows(pid, token.CurrentElementId);

            var isJoin = incoming.Count > 1 && outgoing.Count == 1;
            if (isJoin)
            {
                await HandleMergeBarrier(process, token, defs, pid, ct);
                return;
            }
        }

        if (element is BpmnEndEvent)
        {
            token.Terminate();
            process.RemoveToken(token.Id);
            return;
        }

        // در حالت bypass، توکن بدون انجام هیچ کاری فقط به نود بعدی می‌رود
        await MoveToSingleOrForkByOutgoing(process, token, defs, pid, ct, executableMode: false);
    }

    // -------------------- MERGE (Barrier by incoming flow IDs) --------------------

    private async Task HandleMergeBarrier(
        Process process,
        Token arriving,
        BpmnDefinitionsService defs,
        string pid,
        CancellationToken ct)
    {
        // توکن‌های ورودی منتظر رسیدن به گیت‌وی هستند
        arriving.Wait();

        // اطمینان از وجود scope (برای گروه‌های fork باید وجود داشته باشد)
        if (arriving.ScopeId is null)
        {
            var fallback = arriving.ParentTokenIds.FirstOrDefault();
            arriving.SetScope(fallback != Guid.Empty ? fallback : arriving.Id);
        }

        var scopeId = arriving.ScopeId!.Value;

        var requiredIncoming = defs.GetIncomingSequenceFlows(pid, arriving.CurrentElementId)
            .Select(FlowKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);

        var waiting = allTokens
            .Where(t =>
                t.CurrentElementId == arriving.CurrentElementId &&
                t.State == Domain.ValueObjects.TokenState.Waiting &&
                t.ScopeId == scopeId)
            .ToList();

        var received = waiting
            .Select(t => t.ArrivedViaFlowId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToHashSet();

        // هنوز توکن‌ها برای همه ورودی‌ها نیامده‌اند
        if (!requiredIncoming.All(received.Contains))
            return;

        // در صورتی که همه توکن‌ها رسیدند: انتخاب survivor (معمولاً executable اولویت دارد)
        var survivor = waiting.FirstOrDefault(t => t.IsExecutable) ?? waiting[0];

        foreach (var t in waiting)
        {
            if (t.Id == survivor.Id) continue;
            t.Terminate();
            process.RemoveToken(t.Id);
        }

        // توکن survivor ادامه می‌دهد
        survivor.Resume();
        survivor.ClearArrivedVia();
        survivor.ClearScope();

        var outFlow = defs.GetOutgoingSequenceFlows(pid, arriving.CurrentElementId).SingleOrDefault();
        if (outFlow == null || string.IsNullOrWhiteSpace(outFlow.targetRef))
            throw new InvalidOperationException("Merge gateway must have exactly one outgoing with targetRef.");

        survivor.MoveTo(outFlow.targetRef, FlowKey(outFlow));
    }

    // -------------------- FORK --------------------

    private async Task ForkChildren(
        Process process,
        Token parent,
        List<BpmnSequenceFlow> outgoing,
        CancellationToken ct,
        Func<BpmnSequenceFlow, bool> selectExecutable)
    {
        // والد منتظر می‌ماند و به حالت غیر executable می‌رود
        parent.Wait();
        parent.MarkNonExecutable();

        process.RemoveToken(parent.Id);

        var scopeId = Guid.NewGuid();

        foreach (var flow in outgoing)
        {
            if (string.IsNullOrWhiteSpace(flow.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            var child = new Token(process.Id, flow.targetRef, new[] { parent.Id });
            child.SetScope(scopeId);

            if (!selectExecutable(flow)) child.MarkNonExecutable();

            child.Activate();
            child.MoveTo(child.CurrentElementId, FlowKey(flow));

            await _uow.Tokens.AddAsync(child, ct);
            process.AddToken(child.Id);
        }
    }

    // -------------------- MOVE Helper --------------------

    private async Task MoveToSingleOrForkByOutgoing(
        Process process,
        Token token,
        BpmnDefinitionsService defs,
        string pid,
        CancellationToken ct,
        bool executableMode)
    {
        var outgoing = defs.GetOutgoingSequenceFlows(pid, token.CurrentElementId);

        if (outgoing.Count == 0)
        {
            token.Terminate();
            process.RemoveToken(token.Id);
            return;
        }

        if (outgoing.Count == 1)
        {
            var f = outgoing[0];
            if (string.IsNullOrWhiteSpace(f.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            token.MoveTo(f.targetRef, FlowKey(f));
            return;
        }

        var scopeId = token.ScopeId ?? Guid.NewGuid();

        token.Terminate();
        process.RemoveToken(token.Id);

        foreach (var f in outgoing)
        {
            if (string.IsNullOrWhiteSpace(f.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            var child = new Token(process.Id, f.targetRef, new[] { token.Id });
            child.SetScope(scopeId);

            if (!executableMode) child.MarkNonExecutable();

            child.Activate();
            child.MoveTo(child.CurrentElementId, FlowKey(f)); // set ArrivedViaFlowId

            await _uow.Tokens.AddAsync(child, ct);
            process.AddToken(child.Id);
        }
    }

    // -------------------- Routing / Condition Placeholder --------------------

    private static BpmnSequenceFlow ChooseOneOutgoing(List<BpmnSequenceFlow> outgoing, Token token, Process process)
    {
        // TODO: evaluate conditions using token.Variables + process.Variables
        // For now pick first.
        return outgoing.First();
    }

    private static string FlowKey(BpmnSequenceFlow f)
    {
        // prefer BPMN id; fallback to source->target
        if (!string.IsNullOrWhiteSpace(f.id)) return f.id!;
        return $"{f.sourceRef}->{f.targetRef}";
    }
}
