// Infrastructure/Persistence/Configurations/RuntimeEntityConfigurations.cs
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Infrastructure.Common;
using Novin.Bpmn.Engine.Infrastructure.Persistence.Ef;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

// ------------------------------------------------------------
// Project
// ------------------------------------------------------------
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> entity)
    {
        entity.ToTable("Projects");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.Description)
            .HasMaxLength(2000);

        entity.Property(x => x.IsActive)
            .IsRequired();

        entity.Property(x => x.CreatedAtUtc)
            .IsRequired();

        entity.Property(x => x.UpdatedAtUtc);

        // Unique key per tenant boundary (global uniqueness inside DB here)
        entity.HasIndex(x => x.Key).IsUnique();

        entity.Ignore("DomainEvents");
    }
}

