using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public sealed class TokenForkService : ITokenForkService
{
    private readonly IUnitOfWork _uow;

    public TokenForkService(IUnitOfWork uow)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
    }

    public async Task ForkChildrenAsync(
        Process process,
        Token parent,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        Guid scopeId,
        Func<BpmnSequenceFlow, bool> isExecutableForFlow,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (parent is null) throw new ArgumentNullException(nameof(parent));
        if (outgoing is null) throw new ArgumentNullException(nameof(outgoing));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        foreach (var flow in outgoing)
        {
            if (string.IsNullOrWhiteSpace(flow.targetRef))
                throw new InvalidOperationException("SequenceFlow targetRef is null/empty.");

            // 1) Validate target exists
            var targetElement = ctx.Model.GetElementById(ctx.BpmnProcessId, flow.targetRef);
            if (targetElement == null)
                throw new InvalidOperationException(
                    $"Target element '{flow.targetRef}' not found for flow '{FlowKey(flow)}'.");

            // 2) Create child token
            var child = new Token(process.Id, flow.targetRef, new[] { parent.Id });
            child.SetScope(scopeId);
            child.SetArrivedVia(FlowKey(flow));

            // 3) Inherit parent variables (shallow copy)
            // توجه: Variable Mapping (ApplyInputs) توسط VariableMappingDecorator
            // هنگام اجرای child token انجام می‌شود، نه اینجا (SRP).
            foreach (var kv in parent.Variables)
                child.SetVariable(kv.Key, kv.Value);

            // 4) Mark executable/non-executable based on flow predicate
            if (!isExecutableForFlow(flow))
                child.MarkNonExecutable();

            // 5) Activate child
            // توجه: ApplyInputs در lifecycle بعدی (توسط decorator) اجرا می‌شود
            child.Activate();

            // 6) Persist
            await _uow.Tokens.AddAsync(child, ct);
            process.AddToken(child.Id);
        }
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
