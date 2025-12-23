using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.EventSourcing.Events;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core.Services.Variable;

/// <summary>
/// پیاده‌سازی VariableMergeService
/// </summary>
public class VariableMergeService : IVariableMergeService
{
    public Dictionary<string, object?> MergeVariables(
        IReadOnlyList<ExecutionContext> contexts,
        VariableMergeStrategy strategy = VariableMergeStrategy.LastWriteWins)
    {
        if (contexts == null || contexts.Count == 0)
            return new Dictionary<string, object?>();

        var merged = new Dictionary<string, object?>();

        switch (strategy)
        {
            case VariableMergeStrategy.LastWriteWins:
                // آخرین writer برنده می‌شود (بر اساس ترتیب contexts)
                foreach (var context in contexts)
                {
                    if (context.LocalVariables != null)
                    {
                        foreach (var kv in context.LocalVariables)
                        {
                            merged[kv.Key] = kv.Value;
                        }
                    }
                }
                break;

            case VariableMergeStrategy.FirstWriteWins:
                // اولین writer برنده می‌شود (برعکس ترتیب)
                for (int i = contexts.Count - 1; i >= 0; i--)
                {
                    var context = contexts[i];
                    if (context.LocalVariables != null)
                    {
                        foreach (var kv in context.LocalVariables)
                        {
                            if (!merged.ContainsKey(kv.Key))
                            {
                                merged[kv.Key] = kv.Value;
                            }
                        }
                    }
                }
                break;

            case VariableMergeStrategy.ConflictError:
                // بررسی conflict
                var conflicts = DetectConflicts(contexts);
                if (conflicts.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Variable conflicts detected: {string.Join(", ", conflicts)}");
                }
                // اگر conflict نداشت، مثل LastWriteWins عمل کن
                goto case VariableMergeStrategy.LastWriteWins;

            case VariableMergeStrategy.SingleWriter:
                // فقط یک context می‌تواند متغیر را تغییر دهد
                var allVariables = contexts
                    .Where(c => c.LocalVariables != null)
                    .SelectMany(c => c.LocalVariables!.Keys)
                    .GroupBy(k => k)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (allVariables.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Multiple contexts modified variables: {string.Join(", ", allVariables)}");
                }

                // اگر conflict نداشت، همه متغیرها را merge کن
                foreach (var context in contexts)
                {
                    if (context.LocalVariables != null)
                    {
                        foreach (var kv in context.LocalVariables)
                        {
                            merged[kv.Key] = kv.Value;
                        }
                    }
                }
                break;

            default:
                throw new ArgumentException($"Unknown merge strategy: {strategy}", nameof(strategy));
        }

        return merged;
    }

    public IReadOnlyList<string> DetectConflicts(IReadOnlyList<ExecutionContext> contexts)
    {
        if (contexts == null || contexts.Count < 2)
            return Array.Empty<string>();

        var variableValues = new Dictionary<string, HashSet<object?>>();

        foreach (var context in contexts)
        {
            if (context.LocalVariables == null)
                continue;

            foreach (var kv in context.LocalVariables)
            {
                if (!variableValues.TryGetValue(kv.Key, out var values))
                {
                    values = new HashSet<object?>();
                    variableValues[kv.Key] = values;
                }

                values.Add(kv.Value);
            }
        }

        // متغیرهایی که مقادیر مختلف دارند
        var conflicts = variableValues
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => kv.Key)
            .ToList();

        return conflicts;
    }
}

