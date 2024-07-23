using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Dashbaord
{
    public class EfTaskStorage : ITaskStorage
    {
        private readonly ApplicationDbContext context;

        public EfTaskStorage(ApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task AddTaskAsync(BpmnTask task)
        {
            var novinTask = MapBpmnTaskToNovinTask(task);
            context.Tasks.Add(novinTask);
            await context.SaveChangesAsync();
        }

        public async Task<BpmnTask?> GetTaskByIdAsync(string taskId)
        {
            var novinTask = await context.Tasks
                .Include(t => t.Process)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

            return novinTask == null ? null : MapNovinTaskToBpmnTask(novinTask);
        }

        public async Task<IEnumerable<BpmnTask>> GetTasksForUserAsync(string userId)
        {
            var novinTasks = await context.Tasks
                .Where(t => t.Assignee == userId || t.CandidateByUsers.Contains(userId))
                .Include(t => t.Process)
                .ToListAsync();

            return novinTasks.Select(MapNovinTaskToBpmnTask).ToList();
        }

        public async Task<IEnumerable<BpmnTask>> GetTasksForGroupAsync(string group)
        {
            var novinTasks = await context.Tasks
                .Where(t => t.CandidateByGroups.Contains(group))
                .Include(t => t.Process)
                .ToListAsync();

            return novinTasks.Select(MapNovinTaskToBpmnTask).ToList();
        }

        public async Task ClaimTaskAsync(string taskId, string userId)
        {
            var task = await context.Tasks.FindAsync(taskId);
            if (task != null)
            {
                task.Assignee = userId;
                task.CandidateByUsers = null;
                task.CandidateByGroups = null;
                await context.SaveChangesAsync();
            }
        }

        private NovinTasks MapBpmnTaskToNovinTask(BpmnTask task)
        {
            return new NovinTasks
            {
                Id = Guid.NewGuid(),
                TaskId = task.TaskId,
                Assignee = task.Assignee,
                CandidateByUsers = string.Join(",", task.CandidateUsers),
                CandidateByGroups = string.Join(",", task.CandidateGroups),
                ProcessId = Guid.NewGuid(), // Assign the correct process ID here
                OwnerId = task.Assignee
            };
        }

        private BpmnTask MapNovinTaskToBpmnTask(NovinTasks task)
        {
            return new BpmnTask
            {
                TaskId = task.TaskId,
                Name = task.TaskId, // Or use another field if available
                Assignee = task.Assignee,
                CandidateUsers = task.CandidateByUsers?.Split(',').ToList() ?? new List<string>(),
                CandidateGroups = task.CandidateByGroups?.Split(',').ToList() ?? new List<string>(),
                Status = false // Adjust based on your logic
            };
        }
    }
}
