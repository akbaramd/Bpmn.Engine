using System;
using System.IO;
using System.Threading.Tasks;
using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Xunit;

namespace Novin.Bpmn.Test
{
    public class BpmnV3ProcessInstanceTest
    {
        private readonly string _bpmnFilePath =
            "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

        [Fact]
        public async Task TestBpmnV3InstanceExecution()
        {
            // Load BPMN definition XML
            var definitionXml = await File.ReadAllTextAsync(_bpmnFilePath);
            var processElementId = "process"; // Replace with the actual process ID from your BPMN

            // Initialize the process instance
            var processInstance = new BpmnV3ProcessInstance(processElementId, definitionXml);

            var executor = new BpmnV3ProcessExecutor(processInstance);
            await executor.StartProcessAsync();
            
            
            // Simulate user task completion
            var waitingTokens = processInstance.GetWaitingTokens();
            foreach (var token in waitingTokens)
            {
                Console.WriteLine($"Completing user task for token {token.Id} at {token.CurrentElementId}");
                await executor.CompleteUserTaskAsync(token.Id);
            }
            // Ensure the file exists
            Assert.True(File.Exists(_bpmnFilePath));

            // Additional assertions based on expected token behavior
            Assert.All(processInstance.Tokens, token => 
            {
                Assert.True(token.Status == TokenStatus.Completed || token.Status == TokenStatus.Expired, 
                            $"Token {token.Id} did not reach a valid terminal state.");
            });
        }
    }
}
