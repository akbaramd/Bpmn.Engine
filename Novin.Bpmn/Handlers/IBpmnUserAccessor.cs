namespace Novin.Bpmn.Handlers;

public interface IBpmnUserAccessor
{
    Task<BpmnUser> GetUserByIdAsync(string userId);
    Task<IEnumerable<BpmnUser>> GetUsersByIdsAsync(IEnumerable<string> userIds);
    Task<IEnumerable<BpmnUser>> GetUsersByGroupAsync(string group);
}