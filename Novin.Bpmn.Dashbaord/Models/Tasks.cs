using Microsoft.AspNetCore.Identity;

namespace Novin.Bpmn.Dashbaord.Models;

public class NovinTasks
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public bool IsCompleted { get; set; }
    public Guid ProcessId { get; set; }
    public Process Process { get; set; }
    public string? Assignee { get; set; }
    public string? CandidateByUsers { get; set; }
    public string? CandidateByGroups { get; set; }
    public string? OwnerId { get; set; }
    public string? FormId { get; set; }
    
    public IdentityUser? Owner { get; set; }
    public string DeploymentKey { get; set; }
}