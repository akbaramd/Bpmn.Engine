using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class TokenConfiguration : IEntityTypeConfiguration<Token>
{
    public void Configure(EntityTypeBuilder<Token> entity)
    {
        entity.ToTable("Tokens");

        entity.HasKey(x => x.Id);

        // -------- Correlation --------
        entity.Property(x => x.ProcessId).IsRequired();

        entity.Property(x => x.CurrentElementId)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        // ✅ Current ScopeId persisted + indexed (top-of-stack)
        entity.Property(x => x.ScopeId);
        entity.Property(x => x.ActivityInstanceId);
        entity.Property(x => x.ParentTokenId);

        // ---------------- ScopeStack (private _scopeStack) -> JSON ----------------
        // Expose ScopeStack as read-only (ignore) and persist backing field
        entity.Ignore(x => x.ScopeStack);

        var scopeStackComparer = new ValueComparer<List<Guid>>(
            (a, b) => EfComparers.GuidListEqual(a, b),
            v => EfComparers.GuidListHash(v),
            v => EfComparers.GuidListSnapshot(v));

        entity.Property<List<Guid>>("_scopeStack")
            .HasColumnName("ScopeStack")
            .HasColumnType("text")
            .HasConversion(
                v => JsonHelper.SerializeObject(v ?? new List<Guid>()),
                v => JsonHelper.DeserializeObject<List<Guid>>(v) ?? new List<Guid>())
            .Metadata.SetValueComparer(scopeStackComparer);

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

        // ---------------- Local Variables (private _variables) -> JSON ----------------
      entity.Ignore(x => x.Variables);

        var varsComparer = new ValueComparer<Dictionary<string, JsonNode?>>(
            (a, b) => EfComparers.JsonNodeDictEqual(a, b),
            v => EfComparers.JsonNodeDictHash(v),
            v => EfComparers.JsonNodeDictSnapshot(v));

        entity.Property<Dictionary<string, JsonNode?>>("_variables")
            .HasColumnName("Variables")
            .HasColumnType("text")
            .HasConversion(
                v => JsonVariableCodec.SerializeVars(v),
                v => JsonVariableCodec.DeserializeVars(v))
            .Metadata.SetValueComparer(varsComparer);
        // -------- Timeline --------
        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.ActivatedAt);
        entity.Property(x => x.CompletedAt);

        // -------- Indexes --------
// -------- Indexes --------
entity.HasIndex(x => new { x.ScopeId, x.CurrentElementId, x.State })
    .HasDatabaseName("IX_Token_Scope_Element_State")
    .HasFilter("\"ScopeId\" IS NOT NULL");

entity.HasIndex(x => new { x.ProcessId, x.State })
    .HasDatabaseName("IX_Token_Process_State");

entity.HasIndex(x => new { x.ProcessId, x.CurrentElementId, x.State })
    .HasDatabaseName("IX_Token_Process_Element_State");

entity.HasIndex(x => x.ParentTokenId)
    .HasDatabaseName("IX_Token_ParentTokenId")
    .HasFilter("\"ParentTokenId\" IS NOT NULL");

entity.HasIndex(x => new { x.ProcessId, x.ScopeId })
    .HasDatabaseName("IX_Token_Process_Scope")
    .HasFilter("\"ScopeId\" IS NOT NULL");

entity.HasIndex(x => new { x.ProcessId, x.ActivityInstanceId })
    .HasDatabaseName("IX_Token_Process_ActivityInstance")
    .HasFilter("\"ActivityInstanceId\" IS NOT NULL");


        // -------- Domain events --------
        entity.Ignore("DomainEvents");
    }
}
