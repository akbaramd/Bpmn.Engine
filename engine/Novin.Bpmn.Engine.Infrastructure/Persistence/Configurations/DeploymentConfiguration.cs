using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Infrastructure.Persistence.Configurations;

public sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> entity)
    {
        entity.HasKey(e => e.Id);

        entity.Property(e => e.DeploymentKey).IsRequired().HasMaxLength(500);
        entity.Property(e => e.BpmnXml).IsRequired();
        entity.Property(e => e.Label).HasMaxLength(1000);
        entity.Property(e => e.DeployedAtUtc).IsRequired();
        entity.Property(e => e.UpdatedAtUtc).IsRequired();
        entity.Property(e => e.Version).IsRequired();
        entity.Property(e => e.IsActive).IsRequired();

        entity.Ignore(e => e.DomainEvents);
    }
}