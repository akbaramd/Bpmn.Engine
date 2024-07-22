namespace Novin.Bpmn;

public interface IUserAccessor
{
    Task<BpmnUser> GetUserByIdAsync(string userId);
    Task<IEnumerable<BpmnUser>> GetUsersByIdsAsync(IEnumerable<string> userIds);
    Task<IEnumerable<BpmnUser>> GetUsersByGroupAsync(string group);
}