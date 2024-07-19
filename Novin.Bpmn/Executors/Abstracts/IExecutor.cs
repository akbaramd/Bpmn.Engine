using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors.Abstracts
{
    public interface IExecutor
    {
        Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine);
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