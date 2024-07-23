using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<ITaskStorage, EfTaskStorage>();
builder.Services.AddScoped<IUserAccessor, EfUserAccessor>();
builder.Services.AddScoped<IDefinitionAccessor, EfDefinitionsAccessor>();
builder.Services.AddScoped<IProcessAccsessor, EfProcessAccessor>();
builder.Services.AddScoped<BpmnEngine>();

var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();