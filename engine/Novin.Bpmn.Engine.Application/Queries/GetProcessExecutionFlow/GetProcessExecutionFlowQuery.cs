using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcessExecutionFlow;

/// <summary>
/// Query to get the complete execution flow of a process instance
/// for BPMN visualization in the client
/// </summary>
public sealed record GetProcessExecutionFlowQuery(
    Guid ProcessId
) : IRequest<ProcessExecutionFlowDto>;