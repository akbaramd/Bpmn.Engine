using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Handler for reconstructing the complete execution flow of a process instance
/// </summary>
public sealed class GetProcessExecutionFlowQueryHandler :
    IRequestHandler<GetProcessExecutionFlowQuery, ProcessExecutionFlowDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;

    public GetProcessExecutionFlowQueryHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
    }

    public async Task<ProcessExecutionFlowDto> Handle(
        GetProcessExecutionFlowQuery request,
        CancellationToken ct)
    {
        // Get process
        var process = await _uow.Processes.GetByIdAsync(request.ProcessId, ct);
        if (process == null)
            throw new InvalidOperationException($"Process {request.ProcessId} not found");

        // Get all tokens for this process
        var tokens = await _uow.Tokens.GetByProcessIdAsync(request.ProcessId, ct);

        // Get all boundary subscriptions for this process
        var subscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(request.ProcessId, ct);

        // Get BPMN context for element information
        var bpmnContext = await _ctxFactory.CreateAsync(process, ct);

        // Reconstruct execution flow
        var executedElements = ReconstructExecutedElements(tokens, bpmnContext);
        var executedFlows = ReconstructExecutedFlows(tokens);
        var boundaryEvents = MapBoundaryEvents(subscriptions);
        var executionCycles = ReconstructExecutionCycles(tokens, subscriptions);
        var stats = CalculateStats(process, tokens, executedElements, executedFlows, subscriptions);

        return new ProcessExecutionFlowDto
        {
            ProcessId = process.Id,
            ProcessName = process.Name,
            ProcessDefinitionId = process.ProcessDefinitionId,
            State = process.State,
            CreatedAt = process.CreatedAt,
            CompletedAt = process.CompletedAt,
            ExecutedElements = executedElements,
            ExecutedFlows = executedFlows,
            BoundaryEvents = boundaryEvents,
            ExecutionCycles = executionCycles,
            Stats = stats
        };
    }

    private IReadOnlyCollection<ExecutedElementDto> ReconstructExecutedElements(
        IEnumerable<Token> tokens,
        BpmnRuntimeContext bpmnContext)
    {
        var elementGroups = new Dictionary<string, List<TokenExecutionDto>>();

        foreach (var token in tokens)
        {
            // Current element execution
            if (!string.IsNullOrEmpty(token.CurrentElementId))
            {
                if (!elementGroups.ContainsKey(token.CurrentElementId))
                    elementGroups[token.CurrentElementId] = new List<TokenExecutionDto>();

                elementGroups[token.CurrentElementId].Add(new TokenExecutionDto
                {
                    TokenId = token.Id,
                    ExecutedAt = token.CreatedAt, // Use token creation as execution time
                    ScopeId = token.ScopeId,
                    IsExecutable = token.IsExecutable
                });
            }

            // TODO: If we had token movement history, we could track all elements visited
            // For now, we only track current element
        }

        var result = new List<ExecutedElementDto>();
        foreach (var (elementId, executions) in elementGroups)
        {
            var element = bpmnContext.Model.GetElementById(bpmnContext.BpmnProcessId, elementId);
            var elementType = GetElementType(element);
            var elementName = GetElementName(element);

            result.Add(new ExecutedElementDto
            {
                ElementId = elementId,
                ElementType = elementType,
                ElementName = elementName,
                FirstExecutedAt = executions.Min(e => e.ExecutedAt),
                ExecutionCount = executions.Count,
                TokenExecutions = executions
            });
        }

        return result.OrderBy(e => e.FirstExecutedAt).ToList();
    }

    private IReadOnlyCollection<ExecutedFlowDto> ReconstructExecutedFlows(IEnumerable<Token> tokens)
    {
        var flowGroups = new Dictionary<string, List<TokenExecutionDto>>();

        foreach (var token in tokens)
        {
            if (!string.IsNullOrEmpty(token.ArrivedViaFlowId))
            {
                if (!flowGroups.ContainsKey(token.ArrivedViaFlowId))
                    flowGroups[token.ArrivedViaFlowId] = new List<TokenExecutionDto>();

                flowGroups[token.ArrivedViaFlowId].Add(new TokenExecutionDto
                {
                    TokenId = token.Id,
                    ExecutedAt = token.CreatedAt,
                    ScopeId = token.ScopeId,
                    IsExecutable = token.IsExecutable
                });
            }
        }

        // TODO: Enhance this to get source/target element IDs and flow names
        // For now, we just return flow IDs
        var result = new List<ExecutedFlowDto>();
        foreach (var (flowId, executions) in flowGroups)
        {
            result.Add(new ExecutedFlowDto
            {
                FlowId = flowId,
                SourceElementId = "unknown", // TODO: Map from BPMN model
                TargetElementId = "unknown", // TODO: Map from BPMN model
                FirstExecutedAt = executions.Min(e => e.ExecutedAt),
                ExecutionCount = executions.Count,
                TokenExecutions = executions
            });
        }

        return result.OrderBy(f => f.FirstExecutedAt).ToList();
    }

    private IReadOnlyCollection<BoundaryEventDto> MapBoundaryEvents(IEnumerable<BoundarySubscription> subscriptions)
    {
        return subscriptions.Select(s => new BoundaryEventDto
        {
            SubscriptionId = s.Id,
            AttachedToElementId = s.AttachedToElementId,
            BoundaryEventId = s.BoundaryEventId,
            Kind = s.Kind,
            IsInterrupting = s.IsInterrupting,
            State = s.State,
            ErrorCode = s.ErrorCode,
            TokenScopeId = s.TokenScopeId,
            CreatedAt = s.CreatedAt,
            TriggeredAt = s.TriggeredAt
        }).ToList();
    }

    private IReadOnlyCollection<ExecutionCycleDto> ReconstructExecutionCycles(
        IEnumerable<Token> tokens,
        IEnumerable<BoundarySubscription> subscriptions)
    {
        var cycles = new Dictionary<Guid, ExecutionCycleDto>();

        // Group tokens by scope
        foreach (var token in tokens)
        {
            if (token.ScopeId.HasValue)
            {
                if (!cycles.ContainsKey(token.ScopeId.Value))
                {
                    var tokenCount = tokens.Count(t => t.ScopeId == token.ScopeId.Value);
                    var boundaryCount = subscriptions.Count(s => s.TokenScopeId == token.ScopeId.Value);

                    cycles[token.ScopeId.Value] = new ExecutionCycleDto
                    {
                        ScopeId = token.ScopeId.Value,
                        ScopeName = $"Scope_{token.ScopeId.Value}", // TODO: Get from BPMN model
                        CreatedAt = token.CreatedAt,
                        CompletedAt = tokens.Any(t => t.ScopeId == token.ScopeId.Value && t.State == TokenState.Completed)
                            ? tokens.Where(t => t.ScopeId == token.ScopeId.Value && t.State == TokenState.Completed)
                                .Max(t => t.CompletedAt ?? t.ActivatedAt ?? t.CreatedAt)
                            : null,
                        TokensInScope = tokenCount,
                        BoundaryEventsInScope = boundaryCount
                    };
                }
            }   
        }

        // Boundary events are already counted in the cycle creation above

        return cycles.Values.ToList();
    }

    private ExecutionStatsDto CalculateStats(
        Process process,
        IEnumerable<Token> tokens,
        IReadOnlyCollection<ExecutedElementDto> executedElements,
        IReadOnlyCollection<ExecutedFlowDto> executedFlows,
        IEnumerable<BoundarySubscription> subscriptions)
    {
        var totalExecutionTime = process.CompletedAt.HasValue
            ? (TimeSpan?)(process.CompletedAt.Value - process.CreatedAt)
            : null;

        return new ExecutionStatsDto
        {
            TotalTokens = tokens.Count(),
            ExecutedElements = executedElements.Count,
            ExecutedFlows = executedFlows.Count,
            BoundaryEventsConfigured = subscriptions.Count(),
            BoundaryEventsTriggered = subscriptions.Count(s => s.State == SubscriptionState.Triggered),
            ExecutionCycles = tokens.Select(t => t.ScopeId).Distinct().Count(),
            TotalExecutionTime = totalExecutionTime
        };
    }

    private string GetElementType(object? element)
    {
        if (element == null) return "Unknown";

        return element.GetType().Name switch
        {
            "BpmnStartEvent" => "StartEvent",
            "BpmnEndEvent" => "EndEvent",
            "BpmnUserTask" => "UserTask",
            "BpmnScriptTask" => "ScriptTask",
            "BpmnServiceTask" => "ServiceTask",
            "BpmnBoundaryEvent" => "BoundaryEvent",
            "BpmnIntermediateCatchEvent" => "IntermediateCatchEvent",
            "BpmnIntermediateThrowEvent" => "IntermediateThrowEvent",
            "BpmnExclusiveGateway" => "ExclusiveGateway",
            "BpmnParallelGateway" => "ParallelGateway",
            "BpmnInclusiveGateway" => "InclusiveGateway",
            _ => element.GetType().Name
        };
    }

    private string? GetElementName(object? element)
    {
        if (element == null) return null;

        // Try to get name property using reflection
        var nameProperty = element.GetType().GetProperty("name");
        return nameProperty?.GetValue(element) as string;
    }
}