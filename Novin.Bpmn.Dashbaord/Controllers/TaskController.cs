using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BpmnEngine _engine;
        private readonly UserManager<IdentityUser> _userManager;

        public TaskController(ApplicationDbContext context, UserManager<IdentityUser> userManager, BpmnEngine engine)
        {
            _context = context;
            _userManager = userManager;
            _engine = engine;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User); // Get the current user's ID
            var user = await _userManager.FindByIdAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            var allTasks = await _context.Tasks.ToListAsync();

            var tasks = allTasks.Where(task => task.IsCompleted == false && ( task.Assignee == userId ||
                                                 (task.CandidateByUsers != null && task.CandidateByUsers.Split(',').Contains(userId)) ||
                                                 (task.CandidateByGroups != null && task.CandidateByGroups.Split(',').Any(role => roles.Contains(role)))))
                .ToList();

            return View(tasks);
        }
        
        [HttpPost]
        public async Task<IActionResult> Complete(Guid taskId)
        {
            var task = await _context.Tasks.FindAsync(taskId);
            if (task != null)
            {
                await _engine.CompleteTaskAsync(task.TaskId);
                _context.Update(task);
                await _context.SaveChangesAsync();
                
            }
            
            return RedirectToAction("ProcessDetail","Bpmn",new {id=task.ProcessId});
        }
    }
}