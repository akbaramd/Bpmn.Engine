// Infrastructure/Persistence/Configurations/NodeInstanceConfiguration.cs (final: Variables as single JSON blob)
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
        entity.Property(x => x.Id).ValueGeneratedNever();

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

        entity.Property(x => x.WorkerId);
        entity.Property(x => x.UserTaskId);

        entity.Property(x => x.ErrorMessage)
            .HasMaxLength(4000);

        entity.Property(x => x.IsExecutable)
            .IsRequired();

        // ---------------- ArrivedViaFlowIds (private _arrivedViaFlowIds) -> JSON ----------------
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

        // ---------------- Variables (SINGLE JSON BLOB) ----------------
        // Domain expected:
        //   private string _variablesJson = "{}";
        //   public string VariablesJson => _variablesJson;
        //   public JsonObject VariablesObject => ...
        entity.Ignore("VariablesObject"); // if present as JsonObject property
        entity.Property<string>("_variablesJson")
            .HasColumnName("VariablesJson")
            .HasColumnType("text")
            .IsRequired();

        // ---------------- Indexes ----------------
        entity.HasIndex(x => new { x.ProcessId, x.State })
            .HasDatabaseName("IX_NodeInstance_Process_State");

        entity.HasIndex(x => new { x.ProcessId, x.State, x.CreatedAtUtc })
            .HasDatabaseName("IX_NodeInstance_Process_State_Created");

        entity.HasIndex(x => new { x.ProcessId, x.ElementId })
            .HasDatabaseName("IX_NodeInstance_Process_Element");

        entity.HasIndex(x => new { x.ProcessId, x.ElementId, x.CreatedAtUtc })
            .HasDatabaseName("IX_NodeInstance_Process_Element_Created");

        entity.HasIndex(x => x.TokenId)
            .HasDatabaseName("IX_NodeInstance_TokenId");

        entity.HasIndex(x => new { x.TokenId, x.State })
            .HasDatabaseName("IX_NodeInstance_TokenId_State");

        entity.HasIndex(x => new { x.ProcessId, x.ScopeId })
            .HasDatabaseName("IX_NodeInstance_Process_Scope");

        entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
            .HasDatabaseName("IX_NodeInstance_Process_ActivityInstance");

        entity.HasIndex(x => new { x.WorkerId, x.State })
            .HasDatabaseName("IX_NodeInstance_WorkerId_State");

        entity.Ignore("DomainEvents");
    }
}
