using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class ExecutionFlowRecordConfiguration : IEntityTypeConfiguration<ExecutionFlowRecord>
{
    public void Configure(EntityTypeBuilder<ExecutionFlowRecord> entity)
    {
        entity.ToTable("ExecutionFlowRecords");

        entity.HasKey(x => x.Id);

        // Ordering identity (SQL Server)
        entity.Property(x => x.Position)
            .ValueGeneratedOnAdd();

        entity.Property(x => x.ProcessId).IsRequired();
        entity.Property(x => x.TokenId).IsRequired();

        entity.Property(x => x.FromElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.ToElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.OccurredAtUtc)
            .IsRequired();

        entity.Property(x => x.ScopeId);
        entity.Property(x => x.ActivityInstanceId);

        entity.Property(x => x.EventKey)
            .IsRequired()
            .HasMaxLength(64);

        // ViaFlowIds as JSON (private _viaFlowIds)
        entity.Ignore(x => x.ViaFlowIds);

        var flowIdsComparer = new ValueComparer<List<string>>(
            (a, b) => EfComparers.ListEqual(a, b),
            v => EfComparers.ListHash(v),
            v => EfComparers.ListSnapshot(v));

        entity.Property<List<string>>("_viaFlowIds")
            .HasColumnName("ViaFlowIds")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new List<string>()),
                v => JsonHelper.DeserializeObject<List<string>>(v) ?? new List<string>())
            .Metadata.SetValueComparer(flowIdsComparer);

        // ---------- Indexes ----------
        entity.HasIndex(x => x.EventKey)
            .IsUnique()
            .HasDatabaseName("UX_ExecutionFlow_EventKey");

        entity.HasIndex(x => new { x.ProcessId, x.Position })
            .HasDatabaseName("IX_ExecutionFlow_Process_Position");

        entity.HasIndex(x => new { x.TokenId, x.Position })
            .HasDatabaseName("IX_ExecutionFlow_Token_Position");

        entity.HasIndex(x => new { x.ProcessId, x.OccurredAtUtc })
            .HasDatabaseName("IX_ExecutionFlow_Process_OccurredAtUtc");

        entity.HasIndex(x => new { x.ProcessId, x.ToElementId })
            .HasDatabaseName("IX_ExecutionFlow_Process_ToElement");
        
entity.HasIndex(x => new { x.ProcessId, x.ScopeId, x.Position })
    .HasDatabaseName("IX_ExecutionFlow_Process_Scope_Position")
    .HasFilter("\"ScopeId\" IS NOT NULL");
    }
}
