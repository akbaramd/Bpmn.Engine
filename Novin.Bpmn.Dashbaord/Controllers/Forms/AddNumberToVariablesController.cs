using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Dashbaord.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn.Dashbaord.Controllers.Forms
{
    [UserForm("AddNumberToVariable")]
    public class AddNumberToVariablesController : BaseBpmnController
    {
        public AddNumberToVariablesController(BpmnEngine bpmnEnginep, ApplicationDbContext dbContext, IBpmnTaskAccessor taskAccessor, UserManager<IdentityUser> userManager) : base(bpmnEnginep, dbContext, taskAccessor, userManager)
        {
        }
        

        [HttpGet]
        public override async Task<IActionResult> ShowForm(Guid taskId)
        {
            var task = DbContext.Tasks.First(x=>x.Id == taskId);
            var process = 
                await ProcessEngine.CreateProcessExecutorAsync(task.ProcessId);
            return View(new AddNumberToVariableModel()
            {
                Index = process.Instance.Variables.Index,
                TaskId = taskId
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitForm(AddNumberToVariableModel model)
        {
            if (ModelState.IsValid)
            {
                var task = DbContext.Tasks.First(x=>x.Id == model.TaskId);
                var process = await ProcessEngine.CreateProcessExecutorAsync(task.ProcessId);
                
                process.Instance.Variables.Index = model.Index;
                
                // Call the method from the base class to complete the task and check for new tasks
                return await CompleteTaskAndRedirect(model.TaskId,process.Instance.Variables);
            }
            return View("ShowForm", model);
        }

      
    }

    public class AddNumberToVariableModel
    {
        public Guid TaskId { get; set; }
        public int? Index { get; set; }
    }
}