namespace Novin.Bpmn.Executors.Abstracts
{
    public interface IExecutor
    {
        Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessEngine processEngine);
    }

    public interface IScriptTaskExecutor : IExecutor
    {
    }
    
    public interface IServiceTaskExecutor : IExecutor
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