using System;
using System.Collections.Generic;
using Novin.Bpmn.EventSourcing.Events;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Variable;

/// <summary>
/// Service برای merge کردن متغیرها در join
/// </summary>
public interface IVariableMergeService
{
    /// <summary>
    /// Merge کردن متغیرها از چند ExecutionContext
    /// </summary>
    /// <param name="contexts">ExecutionContextهای که باید merge شوند</param>
    /// <param name="strategy">Strategy برای merge</param>
    /// <returns>Dictionary متغیرهای merge شده</returns>
    Dictionary<string, object?> MergeVariables(
        IReadOnlyList<ExecutionContext> contexts,
        VariableMergeStrategy strategy = VariableMergeStrategy.LastWriteWins);

    /// <summary>
    /// بررسی conflict در متغیرها
    /// </summary>
    /// <param name="contexts">ExecutionContextهای که باید بررسی شوند</param>
    /// <returns>لیست متغیرهایی که conflict دارند</returns>
    IReadOnlyList<string> DetectConflicts(IReadOnlyList<ExecutionContext> contexts);
}

