using Novin.Bpmn.EventSourcing.Contracts;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.EventSourcing.Events;

/// <summary>
/// Event برای تنظیم یک متغیر
/// </summary>
public record VariableSet : BpmnEvent
{
    public override string EventType => nameof(VariableSet);

    /// <summary>
    /// نام متغیر
    /// </summary>
    public required string VariableName { get; init; }

    /// <summary>
    /// مقدار متغیر
    /// </summary>
    public object? VariableValue { get; init; }

    /// <summary>
    /// ExecutionContext ID که این متغیر را تغییر داده است
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Scope: Process یا ExecutionContext
    /// </summary>
    public VariableScope Scope { get; init; } = VariableScope.Process;
}

/// <summary>
/// Event برای تنظیم چند متغیر به صورت batch
/// </summary>
public record VariablesSet : BpmnEvent
{
    public override string EventType => nameof(VariablesSet);

    /// <summary>
    /// Dictionary متغیرها
    /// </summary>
    public required Dictionary<string, object?> Variables { get; init; }

    /// <summary>
    /// ExecutionContext ID که این متغیرها را تغییر داده است
    /// </summary>
    public Guid ExecutionId { get; init; }

    /// <summary>
    /// Scope: Process یا ExecutionContext
    /// </summary>
    public VariableScope Scope { get; init; } = VariableScope.Process;
}

/// <summary>
/// Event برای merge کردن متغیرها در join
/// </summary>
public record VariablesMerged : BpmnEvent
{
    public override string EventType => nameof(VariablesMerged);

    /// <summary>
    /// Dictionary متغیرهای merge شده
    /// </summary>
    public required Dictionary<string, object?> MergedVariables { get; init; }

    /// <summary>
    /// ExecutionContext IDs که merge شده‌اند
    /// </summary>
    public required IReadOnlyList<Guid> MergedExecutionIds { get; init; }

    /// <summary>
    /// ExecutionContext ID جدید که merge شده است
    /// </summary>
    public Guid NewExecutionId { get; init; }

    /// <summary>
    /// Strategy استفاده شده برای merge
    /// </summary>
    public VariableMergeStrategy Strategy { get; init; } = VariableMergeStrategy.LastWriteWins;
}

/// <summary>
/// Scope متغیر
/// </summary>
public enum VariableScope
{
    /// <summary>
    /// متغیر در سطح Process (shared)
    /// </summary>
    Process,

    /// <summary>
    /// متغیر در سطح ExecutionContext (local)
    /// </summary>
    ExecutionContext
}

/// <summary>
/// Strategy برای merge کردن متغیرها در join
/// </summary>
public enum VariableMergeStrategy
{
    /// <summary>
    /// آخرین writer برنده می‌شود (default)
    /// </summary>
    LastWriteWins,

    /// <summary>
    /// اولین writer برنده می‌شود
    /// </summary>
    FirstWriteWins,

    /// <summary>
    /// خطا در صورت conflict
    /// </summary>
    ConflictError,

    /// <summary>
    /// ممنوع - فقط یک context می‌تواند متغیر را تغییر دهد
    /// </summary>
    SingleWriter
}

