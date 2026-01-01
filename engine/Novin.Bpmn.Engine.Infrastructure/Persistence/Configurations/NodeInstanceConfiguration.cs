using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class NodeInstanceConfiguration : IEntityTypeConfiguration<NodeInstance>
{
    public void Configure(EntityTypeBuilder<NodeInstance> entity)
    {
        entity.ToTable("NodeInstances");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();

        entity.Property(x => x.ElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.StartedAtUtc);
        entity.Property(x => x.CompletedAtUtc);

        entity.Property(x => x.ScopeId);
        entity.Property(x => x.ActivityInstanceId);

        // ArrivedViaFlowIds (private _arrivedViaFlowIds) - stored as JSON
        entity.Ignore(x => x.ArrivedViaFlowIds);

        var flowIdsComparer = new ValueComparer<List<string>>(
            (a, b) => EfComparers.ListEqual(a, b),
            v => EfComparers.ListHash(v),
            v => EfComparers.ListSnapshot(v));

        entity.Property<List<string>>("_arrivedViaFlowIds")
            .HasColumnName("ArrivedViaFlowIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new List<string>()),
                v => JsonHelper.DeserializeObject<List<string>>(v) ?? new List<string>())
            .Metadata.SetValueComparer(flowIdsComparer);

        entity.Property(x => x.WorkerId);
        entity.Property(x => x.ErrorMessage).HasMaxLength(4000);

        // Variables (private _variables)
        entity.Ignore(x => x.Variables);

        var varsComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => EfComparers.VarsEqual(a, b),
            v => EfComparers.VarsHash(v),
            v => EfComparers.VarsSnapshot(v));

        entity.Property<Dictionary<string, string>>("_variables")
            .HasColumnName("Variables")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new Dictionary<string, string>(StringComparer.Ordinal)),
                v => JsonHelper.DeserializeObject<Dictionary<string, string>>(v) ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Metadata.SetValueComparer(varsComparer);

        // -------- Indexes --------
        // Hot Query: GetActiveNodeInstances(processId) - Dashboard
        entity.HasIndex(x => new { x.ProcessId, x.State })
            .HasDatabaseName("IX_NodeInstance_Process_State");

        // Hot Query: GetActiveNodeInstances(processId) with ordering
        entity.HasIndex(x => new { x.ProcessId, x.State, x.CreatedAtUtc })
            .HasDatabaseName("IX_NodeInstance_Process_State_Created");

        // Hot Query: GetNodeInstancesForElement(processId, elementId) - Visualization/Trace
        entity.HasIndex(x => new { x.ProcessId, x.ElementId })
            .HasDatabaseName("IX_NodeInstance_Process_Element");

        // Hot Query: GetNodeInstancesForElement with ordering
        entity.HasIndex(x => new { x.ProcessId, x.ElementId, x.CreatedAtUtc })
            .HasDatabaseName("IX_NodeInstance_Process_Element_Created");

        // Hot Query: GetByTokenId(tokenId) - Trace
        entity.HasIndex(x => x.TokenId)
            .HasDatabaseName("IX_NodeInstance_TokenId");

        // Hot Query: GetByTokenId with state filter
        entity.HasIndex(x => new { x.TokenId, x.State })
            .HasDatabaseName("IX_NodeInstance_TokenId_State");

        // Correlation for scope
        entity.HasIndex(x => new { x.ProcessId, x.ScopeId })
            .HasDatabaseName("IX_NodeInstance_Process_Scope")
            .HasFilter("[ScopeId] IS NOT NULL");

        // Activity instance correlation (boundary cancel, subprocess)
        entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
            .HasDatabaseName("IX_NodeInstance_Process_ActivityInstance")
            .HasFilter("[ActivityInstanceId] IS NOT NULL");

        // Tasklist: GetUserTasks(assignee, state) - "My Tasks"
        // Note: If UserTaskInstance table exists separately, these indexes should be there
        // For now, assuming WorkerId can be used for assignee
        entity.HasIndex(x => new { x.WorkerId, x.State })
            .HasDatabaseName("IX_NodeInstance_WorkerId_State")
            .HasFilter("[WorkerId] IS NOT NULL");

        entity.Ignore("DomainEvents");
    }
}