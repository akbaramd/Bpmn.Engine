using System.IO;
using Xunit;

namespace Novin.Bpmn.Test
{
    public class BpmnEngineGatewayTests
    {
        private const string SampleBpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\diagram-variables.bpmn";

        private BpmnEngine CreateEngine(string filePath)
        {
            return new BpmnEngine(filePath);
        }

       
        [Fact]
        public void TestExclusiveGateway_ConditionTrue()
        {
            var engine = CreateEngine(SampleBpmnFilePath);
            engine.Instance.Variables.exampleVar = "Test"; // Set condition for exclusive gateway

      
            Assert.Equal("StartEvent_1af21aa", engine.Instance.CurrentNodeId);

            // Execute the script task to set variable
            engine.ExecuteNext();
            Assert.Equal("ScriptTask_SetVariable", engine.Instance.CurrentNodeId);

            // Execute the next node
            engine.ExecuteNext();
            Assert.Equal("ExclusiveGateway_1", engine.Instance.CurrentNodeId);
            
// Execute the next node
            engine.ExecuteNext();
            Assert.Equal("ScriptTask_PrintVariable", engine.Instance.CurrentNodeId);
            // Rollback and check previous node
            engine.ExecuteNext();
            engine.Rollback();
            Assert.Equal("ExclusiveGateway_1", engine.Instance.CurrentNodeId);

            // Rollback to initial state
            engine.Rollback();
            Assert.Equal("ScriptTask_SetVariable", engine.Instance.CurrentNodeId);

            engine.Rollback();
            Assert.Equal("StartEvent_1af21aa", engine.Instance.CurrentNodeId);
        }

        [Fact]
        public void TestExclusiveGateway_ConditionFalse()
        {
            var engine = CreateEngine(SampleBpmnFilePath);
            engine.Instance.Variables.exampleVar = "NotTest"; // Set condition for exclusive gateway

            // Check the start node
            var startNode = engine.FindStartNode();
            Assert.NotNull(startNode);
            Assert.Equal(startNode.id, "StartEvent_1af21aa");

            // Execute the start node
            engine.ExecuteNext();
            Assert.Equal("ScriptTask_SetVariable", engine.Instance.CurrentNodeId);

            // Execute the script task to set variable
            engine.ExecuteNext();
            Assert.Equal("ExclusiveGateway_1", engine.Instance.CurrentNodeId);

            // Check the next node
            var nextNode = engine.FindNextNode("ExclusiveGateway_1");
            Assert.NotNull(nextNode);
            Assert.Equal("ParallelGateway_1", nextNode?.id);

            // Execute the next node
            engine.ExecuteNext();
            Assert.Equal("ParallelGateway_1", engine.Instance.CurrentNodeId);

            // Rollback and check previous node
            engine.Rollback();
            Assert.Equal("ExclusiveGateway_1", engine.Instance.CurrentNodeId);

            // Rollback to initial state
            engine.Rollback();
            Assert.Equal("ScriptTask_SetVariable", engine.Instance.CurrentNodeId);

            engine.Rollback();
            Assert.Equal("StartEvent_1af21aa", engine.Instance.CurrentNodeId);
        }
    }
}
