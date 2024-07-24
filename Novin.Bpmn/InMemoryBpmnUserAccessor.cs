using Novin.Bpmn.Handlers;

namespace Novin.Bpmn
{
    public class InMemoryBpmnUserAccessor : IBpmnUserAccessor
    {
        private readonly Dictionary<string, BpmnUser> users = new Dictionary<string, BpmnUser>();
        private readonly Dictionary<string, List<BpmnUser>> groups = new Dictionary<string, List<BpmnUser>>();

        public InMemoryBpmnUserAccessor()
        {
            // Initialize with some sample users
            var user1 = new BpmnUser { Id = "1", Name = "Alice", Group = "GroupA" };
            var user2 = new BpmnUser { Id = "2", Name = "Bob", Group = "GroupA" };
            var user3 = new BpmnUser { Id = "3", Name = "Charlie", Group = "GroupB" };
            var user4 = new BpmnUser { Id = "4", Name = "Dave", Group = "GroupB" };

            users[user1.Id] = user1;
            users[user2.Id] = user2;
            users[user3.Id] = user3;
            users[user4.Id] = user4;

            if (!groups.ContainsKey(user1.Group))
                groups[user1.Group] = new List<BpmnUser>();
            groups[user1.Group].Add(user1);

            if (!groups.ContainsKey(user2.Group))
                groups[user2.Group] = new List<BpmnUser>();
            groups[user2.Group].Add(user2);

            if (!groups.ContainsKey(user3.Group))
                groups[user3.Group] = new List<BpmnUser>();
            groups[user3.Group].Add(user3);

            if (!groups.ContainsKey(user4.Group))
                groups[user4.Group] = new List<BpmnUser>();
            groups[user4.Group].Add(user4);
        }

        public Task<string> GetUserNameAsync(string userId)
        {
            if (users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(user.Name);
            }
            return Task.FromResult("Unknown User");
        }

        public Task<BpmnUser> GetUserByIdAsync(string userId)
        {
            if (users.TryGetValue(userId, out var user))
            {
                return Task.FromResult(user);
            }
            throw new Exception("User not found");
        }

        public Task<IEnumerable<BpmnUser>> GetUsersByIdsAsync(IEnumerable<string> userIds)
        {
            var result = users.Values.Where(user => userIds.Contains(user.Id));
            return Task.FromResult(result);
        }

        public Task<IEnumerable<BpmnUser>> GetUsersByGroupAsync(string group)
        {
            if (groups.TryGetValue(group, out var groupUsers))
            {
                return Task.FromResult(groupUsers.AsEnumerable());
            }
            return Task.FromResult(Enumerable.Empty<BpmnUser>());
        }
    }
}
