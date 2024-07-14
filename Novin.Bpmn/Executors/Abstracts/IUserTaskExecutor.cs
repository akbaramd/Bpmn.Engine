using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors.Abstracts;

public interface IUserTaskExecutor
{
    void Execute(BpmnUserTask userTask, BpmnInstance instance);
}