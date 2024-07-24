using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord.Accessors
{
    public class EfBpmnTasksAccessor : IBpmnTaskAccessor
    {
        private readonly ApplicationDbContext context;

        public EfBpmnTasksAccessor(ApplicationDbContext context)
        {
            this.context = context;
        }

        public async Task StoreTask(BpmnTask task)
        {
            var find = await context.Tasks.FirstOrDefaultAsync(x => x.TaskId.Equals(task.TaskId));
            if (find == null)
            {   
                var novinTask = MapBpmnTaskToNovinTask(task);
                context.Tasks.Add(novinTask);
                await context.SaveChangesAsync();
            }
            else
            {
                find.Name = task.Name;
                find.Assignee = task.Assignee;
                find.CandidateByUsers = string.Join(",", task.CandidateUsers);
                find.CandidateByGroups = string.Join(",", task.CandidateGroups);
                find.ProcessId = task.ProcessId;
                find.DeploymentKey = task.DeploymentKey;
                find.OwnerId = task.Assignee;
                context.Tasks.Update(find);
                await context.SaveChangesAsync();
            }
            
        }

        public async Task<BpmnTask?> RetrieveTask(string taskId)
        {
            var task = await context.Tasks
                .Include(t => t.Process)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

            return task == null ? null : MapNovinTaskToBpmnTask(task);
        }

        private NovinTasks MapBpmnTaskToNovinTask(BpmnTask task)
        {
            var process = context.Processes.First(x => x.Id.Equals(task.ProcessId));
            return new NovinTasks
            {
                Id = Guid.NewGuid(),
                TaskId = task.TaskId,
                Name = task.Name,
                Assignee = task.Assignee,
                IsCompleted = task.IsCompleted,
                CandidateByUsers = task.CandidateUsers != null ? string.Join(",", task.CandidateUsers) : null,
                CandidateByGroups = task.CandidateGroups != null ? string.Join(",", task.CandidateGroups) : null,
                ProcessId = process.Id,
                DeploymentKey = task.DeploymentKey,
                OwnerId = task.Assignee
            };
        }

        private BpmnTask MapNovinTaskToBpmnTask(NovinTasks task)
        {
            var customTask = new BpmnTask(
                taskId: task.TaskId,
                name: task.Name,
                assignee: task.Assignee,
                processId: task.ProcessId,
                deploymentKey: task.DeploymentKey);

            customTask.IsCompleted = task.IsCompleted;

            if (!string.IsNullOrEmpty(task.CandidateByUsers))
            {
                customTask.AddCandidateUsers(task.CandidateByUsers.Split(','));
            }

            if (!string.IsNullOrEmpty(task.CandidateByGroups))
            {
                customTask.AddCandidateGroups(task.CandidateByGroups.Split(','));
            }

            return customTask;
            
        }
    }
}
