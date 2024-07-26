namespace Novin.Bpmn.Dashbaord;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class UserFormAttribute : Attribute
{
    public string FormName { get; }

    public UserFormAttribute(string formName)
    {
        FormName = formName;
    }
}