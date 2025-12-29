namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Represents a variable with its type information for SignalR transmission
/// </summary>
public class TypedVariable
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string TypeName { get; set; } = string.Empty;
    
    public TypedVariable() { }
    
    public TypedVariable(string key, object? value)
    {
        Key = key;
        Value = value;
        TypeName = value?.GetType().FullName ?? "System.Object";
    }
}
