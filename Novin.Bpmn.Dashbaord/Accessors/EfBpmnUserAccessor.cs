using Microsoft.AspNetCore.Identity;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Handlers;

namespace Novin.Bpmn.Dashbaord.Accessors;

public class EfBpmnUserAccessor : IBpmnUserAccessor
{
    private readonly ApplicationDbContext context;
    private readonly UserManager<IdentityUser> userManager;
    private readonly RoleManager<IdentityRole> roleManager;

    public EfBpmnUserAccessor(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        this.context = context;
        this.userManager = userManager;
        this.roleManager = roleManager;
    }

    public async Task<BpmnUser> GetUserByIdAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var group = roles.FirstOrDefault(); // Assuming the first role is the group

        var bpmnUser = new BpmnUser
        {
            Id = user.Id,
            Name = user.UserName,
            Group = group
        };

        return bpmnUser;
    }

    public async Task<IEnumerable<BpmnUser>> GetUsersByIdsAsync(IEnumerable<string> userIds)
    {
        var users = new List<BpmnUser>();
        foreach (var userId in userIds)
        {
            var user = await GetUserByIdAsync(userId);
            if (user != null)
            {
                users.Add(user);
            }
        }

        return users;
    }

    public async Task<IEnumerable<BpmnUser>> GetUsersByGroupAsync(string group)
    {
        var usersInGroup = new List<BpmnUser>();

        var role = await roleManager.FindByNameAsync(group);
        if (role != null)
        {
            var userRoles = context.UserRoles.Where(ur => ur.RoleId == role.Id).ToList();
            foreach (var userRole in userRoles)
            {
                var user = await userManager.FindByIdAsync(userRole.UserId);
                if (user != null)
                {
                    usersInGroup.Add(new BpmnUser
                    {
                        Id = user.Id,
                        Name = user.UserName,
                        Group = group
                    });
                }
            }
        }

        return usersInGroup;
    }
}