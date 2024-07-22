using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Defiantions> Definations { get; set; }
    public DbSet<Process> Processes { get; set; }
}