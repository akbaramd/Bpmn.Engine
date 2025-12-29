using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.ClientDemo.Handlers;

/// <summary>
/// UserTask handler that collects two numbers from user input
/// </summary>
[BpmnWorker(
    workerId: "ui-user-task-worker",
    workType: "UserTask",
    Name = "User Task UI Handler",
    Description = "Handles user input for number entry")]
public class SumUserTaskHandler : BpmnWorkerHandler
{
    private readonly ILogger<SumUserTaskHandler> _logger;

    public SumUserTaskHandler(ILogger<SumUserTaskHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string HandlerId =>
        GetBpmnWorkerAttribute()?.WorkerId ?? GetType().Name;

    public override string WorkType =>
        GetBpmnWorkerAttribute()?.WorkType
        ?? throw new InvalidOperationException("WorkType not defined");

    public override async Task ExecuteAsync(
        WorkerContext workItem,
        CancellationToken cancellationToken = default)
    {
      

        // 🧪 شبیه‌سازی ورودی کاربر
        double number1 = 15.5;
        double number2 = 24.7;

        _logger.LogInformation(
            "User entered values: number1={Number1}, number2={Number2}",
            number1,
            number2);

        // 🔑 ست کردن خروجی فرم
        workItem.Variables.SetDouble("number1", number1);
        workItem.Variables.SetDouble("number2", number2);

        // شبیه‌سازی تاخیر UI
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation(
            "UserTask input collected successfully for Job {WorkerId}",
            workItem.WorkerId);
    }

    private BpmnWorkerAttribute? GetBpmnWorkerAttribute()
    {
        return Attribute.GetCustomAttribute(
            GetType(),
            typeof(BpmnWorkerAttribute)) as BpmnWorkerAttribute;
    }
}
