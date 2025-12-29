using Novin.Bpmn.Engine.Clients.Abstractions;

namespace Novin.Bpmn.Engine.ClientDemo.Handlers;

/// <summary>
/// Sample service handler for mathematical operations
/// </summary>
[BpmnWorker("math-worker", "ServiceTask", Name = "Math Calculator", Description = "Performs mathematical calculations")]
public class MathHandler : BpmnWorkerHandler
{
    private readonly ILogger<MathHandler> _logger;

    public MathHandler(ILogger<MathHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override string HandlerId => GetBpmnWorkerAttribute()?.WorkerId ?? GetType().Name;
    public override string WorkType => GetBpmnWorkerAttribute()?.WorkType ?? throw new InvalidOperationException("WorkType not defined in BpmnWorker attribute");

    private BpmnWorkerAttribute? GetBpmnWorkerAttribute()
    {
        return Attribute.GetCustomAttribute(GetType(), typeof(BpmnWorkerAttribute)) as BpmnWorkerAttribute;
    }

    public override async Task ExecuteAsync(WorkerContext workItem, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing math work item: {WorkItemId}", workItem.WorkerId);

        try
        {


            // Convert to numbers (supporting int, double, decimal)
            double number1 = workItem.Variables.GetDouble("number1");
            double number2 = workItem.Variables.GetDouble("number2");

            // Perform addition
            double sum = number1 + number2;

            _logger.LogInformation("Calculated sum: {Number1} + {Number2} = {Sum}", number1, number2, sum);

            // Store the result in the work item for return to BPMN process
            workItem.Variables.SetDouble("sum",sum);

            // Simulate processing delay
            await Task.Delay(100, cancellationToken);

            _logger.LogInformation("Math operation completed successfully. Result stored: {Sum}", sum);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process math work item: {WorkItemId}", workItem.WorkerId);
            throw;
        }
    }

    private double ConvertToDouble(object value)
    {
        return value switch
        {
            int i => (double)i,
            long l => (double)l,
            float f => (double)f,
            double d => d,
            decimal m => (double)m,
            string s => double.Parse(s),
            _ => throw new ArgumentException($"Cannot convert {value.GetType()} to double")
        };
    }
}