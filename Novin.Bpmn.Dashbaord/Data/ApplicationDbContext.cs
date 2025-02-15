﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Definitions> Definitions { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<NovinTasks> Tasks { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<NovinTasks>().HasOne(x => x.Process).WithMany()
            .HasForeignKey(x => x.ProcessId);
        
        builder.Entity<NovinTasks>().HasOne(x => x.Owner).WithMany()
            .HasForeignKey(x => x.OwnerId);
        builder.Entity<Process>().HasOne(x => x.Definition).WithMany()
            .HasForeignKey(x => x.DefinitionId);

        builder.Entity<NovinTasks>().Property(x => x.OwnerId).IsRequired(false);
        builder.Entity<NovinTasks>().Property(x => x.ProcessId).IsRequired();
        builder.Entity<Process>().Property(x => x.DefinitionId).IsRequired();

        builder.Entity<NovinTasks>().HasIndex(x => x.Assignee);
        builder.Entity<NovinTasks>().HasIndex(x => x.CandidateByGroups);
        builder.Entity<NovinTasks>().HasIndex(x => x.CandidateByUsers);
        builder.Entity<NovinTasks>().HasKey(x => x.Id);
    }
}