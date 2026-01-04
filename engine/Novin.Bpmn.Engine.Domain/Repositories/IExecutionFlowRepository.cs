using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Common.Interfaces;

public interface IExecutionFlowRepository
{
    Task AddAsync(ExecutionFlowRecord record, CancellationToken ct);

    Task<IReadOnlyList<ExecutionFlowRecord>> GetByProcessIdAsync(Guid processId, CancellationToken ct);
    Task<IReadOnlyList<ExecutionFlowRecord>> GetByTokenIdAsync(Guid tokenId, CancellationToken ct);

    Task<bool> ExistsByEventKeyAsync(string eventKey, CancellationToken ct);
    Task<long> GetNextPositionAsync(Guid processId, CancellationToken ct);
}
