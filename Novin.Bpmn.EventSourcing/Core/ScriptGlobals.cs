using System.Collections.Generic;
using System.Dynamic;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Globals object for C# script execution in EventSourcing engine.
/// Uses ExpandoObject as the base to enable direct property access in scripts.
/// Scripts can use:
/// - Direct access: var x = baseScore; (for input-mapped variables)
/// - Property syntax: Variables.resultScore = value; (for setting output variables)
/// 
/// Note: This class wraps ExpandoObject and adds a Variables property that points to itself,
/// allowing both direct access and Variables.property syntax.
/// </summary>
public class ScriptGlobals : DynamicObject
{
    private readonly ExecutionContext _context;
    private readonly Dictionary<string, object?> _variables;
    private readonly ExpandoObject _expando;

    public ScriptGlobals(ExecutionContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _variables = context.LocalVariables ?? new Dictionary<string, object?>();
        
        // Create ExpandoObject with all variables as direct properties
        _expando = new ExpandoObject();
        var expandoDict = (IDictionary<string, object?>)_expando;
        
        // Initialize ExpandoObject with existing variables (enables direct access like: baseScore)
        foreach (var kv in _variables)
        {
            expandoDict[kv.Key] = kv.Value;
        }
        
        // Add Variables property that points to the same ExpandoObject
        // This allows both direct access (baseScore) and Variables.property syntax
        expandoDict["Variables"] = _expando;
    }

    /// <summary>
    /// ExpandoObject for setting/getting variables in scripts using property syntax.
    /// Scripts can use: Variables.resultScore = value; or Variables["resultScore"] = value;
    /// Also enables direct access: var x = baseScore;
    /// </summary>
    public dynamic Variables => _expando;

    /// <summary>
    /// Direct access to local variables dictionary.
    /// </summary>
    public Dictionary<string, object?> LocalVariables => _variables;

    /// <summary>
    /// Execution context for advanced access if needed.
    /// </summary>
    public ExecutionContext Context => _context;

    /// <summary>
    /// Enables direct property access to variables in scripts via DynamicObject.
    /// Example: var x = baseScore; (works because ExpandoObject is used internally)
    /// </summary>
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        var name = binder.Name;
        var expandoDict = (IDictionary<string, object?>)_expando;
        
        // Check in ExpandoObject (which contains all variables)
        if (expandoDict.TryGetValue(name, out result))
        {
            return true;
        }
        
        result = null;
        return false;
    }

    /// <summary>
    /// Enables direct property assignment to variables in scripts.
    /// Example: baseScore = 100; (sets both ExpandoObject and dictionary)
    /// </summary>
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        var name = binder.Name;
        
        // Don't allow overwriting Variables property
        if (name == "Variables")
        {
            return false;
        }
        
        // Set in both ExpandoObject and dictionary
        var expandoDict = (IDictionary<string, object?>)_expando;
        expandoDict[name] = value;
        _variables[name] = value;
        
        return true;
    }

    /// <summary>
    /// Syncs ExpandoObject changes back to the dictionary.
    /// Call this after script execution to ensure all changes are captured.
    /// </summary>
    public void SyncVariablesFromExpando()
    {
        var expandoDict = (IDictionary<string, object?>)_expando;
        foreach (var kv in expandoDict)
        {
            // Skip the Variables property itself
            if (kv.Key == "Variables")
            {
                continue;
            }
            _variables[kv.Key] = kv.Value;
        }
    }
    
    /// <summary>
    /// Gets the underlying ExpandoObject for direct use as globals.
    /// This allows Roslyn to see all properties at compile-time.
    /// </summary>
    public ExpandoObject GetExpandoObject() => _expando;
}

