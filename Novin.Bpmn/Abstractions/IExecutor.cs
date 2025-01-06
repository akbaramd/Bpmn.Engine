namespace Novin.Bpmn.Executors.Abstracts
{
    public interface IExecutor
    {
        Task ExecuteAsync(BpmnProcessNode processNode, BpmnProcessExecutor processExecutor);
    }
}