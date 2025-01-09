namespace Novin.Bpmn.Abstractions
{
    public interface IExecutor
    {
        Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor);
    }
}