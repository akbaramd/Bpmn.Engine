using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord;
using Novin.Bpmn.Dashbaord.Accessors;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Handlers;

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

builder.Services.AddScoped<IBpmnTaskAccessor, EfBpmnTasksAccessor>();
builder.Services.AddScoped<IBpmnUserAccessor, EfBpmnUserAccessor>();
builder.Services.AddScoped<IBpmnDefinitionAccessor, EfBpmnDefinitionsAccessor>();
builder.Services.AddScoped<IBpmnProcessAccessor, EfBpmnProcessAccessor>();
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
    name: "UserForms",
    pattern: "user-task/{taskId}",
    defaults: new { action = "ShowForm" ,controller="AddNumberToVariables"},
    constraints: new { taskId = new UserFormRouteConstraint(app.Services) });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");



app.MapRazorPages();

app.Run();