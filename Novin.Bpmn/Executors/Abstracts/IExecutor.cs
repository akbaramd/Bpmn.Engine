namespace Novin.Bpmn.Executors.Abstracts
{
    public interface IExecutor
    {
        Task ExecuteAsync(BpmnNode node, BpmnEngine engine);
    }

    public interface ITaskExecutor : IExecutor
    {
    }

    public interface IGatewayExecutor : IExecutor
    {
    }

    public interface IUserTaskExecutor : IExecutor
    {
    }

    public interface IStartEventExecutor : IExecutor
    {
    }

    public interface IEndEventExecutor : IExecutor
    {
    }
}