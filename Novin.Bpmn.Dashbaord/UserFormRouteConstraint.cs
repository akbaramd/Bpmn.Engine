using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Dashbaord;
using Novin.Bpmn.Dashbaord.Data;

public class UserFormRouteConstraint : IRouteConstraint
{
    private readonly IServiceProvider _serviceProvider;

    public UserFormRouteConstraint(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        try
        {
            if (values.TryGetValue(routeKey, out var value) && value is string formId)
            {
                var userTaskService = _serviceProvider.CreateScope().ServiceProvider
                    .GetRequiredService<ApplicationDbContext>();
                var userTask = userTaskService?.Tasks.FirstOrDefault(x => x.Id == Guid.Parse(formId));

                if (userTask == null) return false;

                var assembly = Assembly.GetExecutingAssembly();
                var controllers = assembly.GetTypes().Where(type => typeof(Controller).IsAssignableFrom(type));

                foreach (var controller in controllers)
                {
                    var attribute = controller.GetCustomAttribute<UserFormAttribute>();
                    if (attribute != null &&
                        attribute.FormName.Equals(userTask.FormId, StringComparison.OrdinalIgnoreCase))
                    {
                        values["controller"] = controller.Name.Replace("Controller", "");
                        values["action"] = "ShowForm";
                        values["taskId"] = formId;
                        return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
        return false;
    }
}