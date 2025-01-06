using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;

namespace Novin.Bpmn.Dashbaord.Controllers;

public abstract class BaseBpmnController: Controller
{
    private readonly BpmnEngine bpmnEnginep;
    private readonly ApplicationDbContext dbContext;
    private readonly IBpmnTaskAccessor taskAccessor;
    private readonly UserManager<IdentityUser> userManager;

    public BpmnEngine ProcessEngine => bpmnEnginep;
    public ApplicationDbContext DbContext => dbContext;

    protected BaseBpmnController(BpmnEngine bpmnEnginep, ApplicationDbContext dbContext, IBpmnTaskAccessor taskAccessor, UserManager<IdentityUser> userManager)
    {
        this.bpmnEnginep = bpmnEnginep;
        this.dbContext = dbContext;
        this.taskAccessor = taskAccessor;
        this.userManager = userManager;
    }

    public abstract Task<IActionResult> ShowForm(Guid taskId);
    
    
    protected async Task<IActionResult> CompleteTaskAndRedirect(Guid taskId, dynamic? variables = null)
    {
        var res = await ProcessEngine.CompleteUserTaskAsync(taskId,variables);
        
        // Check for new tasks after the current process is completed

        var user = await userManager.GetUserAsync(User);
        var task = await taskAccessor.RetrieveUserTask(user.Id, res.Id);
        
        if (task != null)
        {
            // Redirect to the new task form
            return Redirect($"/user-task/{task.TaskId}");
        }

        // If no new tasks, redirect to a default page or a confirmation page
            return Redirect("/");
    }
}