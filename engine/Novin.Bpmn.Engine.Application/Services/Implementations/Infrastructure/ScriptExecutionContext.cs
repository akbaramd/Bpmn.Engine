namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Script execution context containing only IDs and system variables.
/// Uses BonyanVariables which extends Dictionary&lt;string, string&gt; with typed methods.
/// </summary>
public sealed class ScriptExecutionContext
{
    /// <summary>
    /// Process instance ID
    /// </summary>
    public Guid ProcessId { get; }

    /// <summary>
    /// Token ID
    /// </summary>
    public Guid TokenId { get; }

    /// <summary>
    /// System variables - BonyanVariables extends Dictionary&lt;string, string&gt; with typed setter/getter methods.
    /// All variable values are stored as strings internally.
    /// </summary>
    public BonyanVariables Variables { get; }

   
    public ScriptExecutionContext(Guid processId, Guid tokenId, Dictionary<string, string>? initialVariables = null)
    {
        ProcessId = processId;
        TokenId = tokenId;
        Variables = initialVariables != null 
            ? new BonyanVariables(initialVariables) 
            : new BonyanVariables();
    }
}