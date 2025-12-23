using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing.Core.Services;

/// <summary>
/// Service for applying Bonyan IO mappings between process variables and node variables.
/// Follows Zeebe-style semantics: inputs applied on activation, outputs applied on completion.
/// </summary>
public sealed class IoMappingApplier
{
    /// <summary>
    /// Result of applying IO mappings, containing the applied pairs for event sourcing.
    /// </summary>
    public sealed record MappingResult(
        IReadOnlyList<VariableMapping> AppliedMappings,
        IReadOnlyList<string> Errors
    );

    /// <summary>
    /// Represents a single variable mapping that was applied.
    /// </summary>
    public sealed record VariableMapping(
        string SourceVariable,
        string TargetVariable,
        object? Value
    );

    /// <summary>
    /// Applies input mappings: Process Variables → Node Variables
    /// Called when a node is activated.
    /// </summary>
    /// <param name="mapping">The IO mapping configuration.</param>
    /// <param name="processVariables">Process-level variables (source).</param>
    /// <param name="nodeVariables">Node-level variables (target).</param>
    /// <returns>Result containing applied mappings and any errors.</returns>
    public MappingResult ApplyInputs(
        BonyanIoMapping mapping,
        IDictionary<string, object?> processVariables,
        IDictionary<string, object?> nodeVariables)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        if (processVariables == null)
            throw new ArgumentNullException(nameof(processVariables));
        if (nodeVariables == null)
            throw new ArgumentNullException(nameof(nodeVariables));

        var appliedMappings = new List<VariableMapping>();
        var errors = new List<string>();

        foreach (var input in mapping.Input ?? Enumerable.Empty<BonyanIoMappingInput>())
        {
            if (string.IsNullOrWhiteSpace(input.Source) || string.IsNullOrWhiteSpace(input.Target))
            {
                errors.Add($"Invalid input mapping: source='{input.Source}', target='{input.Target}'");
                continue;
            }

            // Extract variable name from FEEL expression (support "=variableName" or "variableName")
            var sourceVarName = ExtractVariableName(input.Source);

            if (!processVariables.TryGetValue(sourceVarName, out var value))
            {
                switch (mapping.OnMissingSource)
                {
                    case MissingBehavior.Null:
                        nodeVariables[input.Target] = null;
                        appliedMappings.Add(new VariableMapping(sourceVarName, input.Target, null));
                        break;

                    case MissingBehavior.Fail:
                        errors.Add($"Missing process variable '{sourceVarName}' for input mapping target '{input.Target}'");
                        break;

                    case MissingBehavior.Skip:
                    default:
                        // Skip this mapping
                        break;
                }
                continue;
            }

            // Apply the mapping
            nodeVariables[input.Target] = value;
            appliedMappings.Add(new VariableMapping(sourceVarName, input.Target, value));
        }

        return new MappingResult(appliedMappings, errors);
    }

    /// <summary>
    /// Applies output mappings: Node Variables → Process Variables
    /// Called when a node is completed.
    /// </summary>
    /// <param name="mapping">The IO mapping configuration.</param>
    /// <param name="nodeVariables">Node-level variables (source).</param>
    /// <param name="processVariables">Process-level variables (target).</param>
    /// <returns>Result containing applied mappings and any errors.</returns>
    public MappingResult ApplyOutputs(
        BonyanIoMapping mapping,
        IDictionary<string, object?> nodeVariables,
        IDictionary<string, object?> processVariables)
    {
        if (mapping == null)
            throw new ArgumentNullException(nameof(mapping));
        if (nodeVariables == null)
            throw new ArgumentNullException(nameof(nodeVariables));
        if (processVariables == null)
            throw new ArgumentNullException(nameof(processVariables));

        var appliedMappings = new List<VariableMapping>();
        var errors = new List<string>();

        foreach (var output in mapping.Output ?? Enumerable.Empty<BonyanIoMappingOutput>())
        {
            if (string.IsNullOrWhiteSpace(output.Source) || string.IsNullOrWhiteSpace(output.Target))
            {
                errors.Add($"Invalid output mapping: source='{output.Source}', target='{output.Target}'");
                continue;
            }

            if (!nodeVariables.TryGetValue(output.Source, out var value))
            {
                switch (mapping.OnMissingOutput)
                {
                    case MissingBehavior.Fail:
                        errors.Add($"Missing node variable '{output.Source}' for output mapping target '{output.Target}'");
                        break;

                    case MissingBehavior.Null:
                        // Set target to null
                        if (mapping.Overwrite || !processVariables.ContainsKey(output.Target))
                        {
                            processVariables[output.Target] = null;
                            appliedMappings.Add(new VariableMapping(output.Source, output.Target, null));
                        }
                        break;

                    case MissingBehavior.Skip:
                    default:
                        // Skip this mapping
                        break;
                }
                continue;
            }

            // Check overwrite policy
            if (!mapping.Overwrite && processVariables.ContainsKey(output.Target))
            {
                // Skip overwriting existing variable
                continue;
            }

            // Apply the mapping
            processVariables[output.Target] = value;
            appliedMappings.Add(new VariableMapping(output.Source, output.Target, value));
        }

        return new MappingResult(appliedMappings, errors);
    }

    /// <summary>
    /// Extracts variable name from a FEEL expression.
    /// Supports both "=variableName" (Zeebe style) and "variableName" formats.
    /// For now, only simple variable references are supported (no complex expressions).
    /// </summary>
    private static string ExtractVariableName(string feelExpression)
    {
        if (string.IsNullOrWhiteSpace(feelExpression))
            return feelExpression;

        // Remove leading "=" if present (Zeebe style)
        var trimmed = feelExpression.Trim();
        if (trimmed.StartsWith("=", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(1).Trim();
        }

        // For now, only support simple identifiers (no path expressions like "order.total")
        // This can be extended later to support FEEL path expressions
        return trimmed;
    }
}

