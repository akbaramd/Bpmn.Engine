using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Script globals for script execution.
/// Provides access to ScriptExecutionContext with BonyanVariables.
/// </summary>
public sealed class ScriptGlobals
{
    /// <summary>
    /// Script execution context with Variables (BonyanVariables) and typed setter methods
    /// </summary>
    public ScriptExecutionContext Context { get; }

    public ScriptGlobals(Process process, Token token)
    {
        if (process == null) throw new ArgumentNullException(nameof(process));
        if (token == null) throw new ArgumentNullException(nameof(token));
        var initialVariables = token.Variables.ToDictionary(
            kv => kv.Key,
            kv => (object?)JsonVariableCodec.CloneNode(kv.Value),
            StringComparer.Ordinal);

        Context = new ScriptExecutionContext(process.Id, token.Id, initialVariables);
    }

    /// <summary>
    /// Syncs Variables back to the token after script execution
    /// </summary>
    public void SyncToToken(Token token)
    {
        if (token == null) throw new ArgumentNullException(nameof(token));
        
        foreach (var kvp in Context.Variables)
        {
            token.SetVariable(kvp.Key, kvp.Value);
        }
    }
}