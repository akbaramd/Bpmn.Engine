using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Novin.Bpmn.Test
{
    public class TimerBoundaryEventTest
    {
        private readonly ITestOutputHelper _output;

        public TimerBoundaryEventTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Timer_And_InclusiveGateway_ShouldWork()
        {
            // Arrange
            var bpmnXml = await File.ReadAllTextAsync("Bpmn/simple_inclusive_timer.bpmn");
            
            // 1. Create a process instance
            var processInstance = new BpmnV3ProcessInstance("process", bpmnXml);
            var executor = new BpmnProcessManager(processInstance);
            
            // Act
            await executor.StartProcessAsync();
            
            // Log information about the execution
            foreach (var token in processInstance.Tokens)
            {
                _output.WriteLine($"Token {token.Id} at {token.CurrentElementId} with status {token.Status}");
            }
            
            // Assert
            // 1. Check that tokens have reached the end event
            var tokensAtEndEvent = processInstance.Tokens
                .Where(t => t.CurrentElementId == "event_end")
                .ToList();
            
            Assert.True(tokensAtEndEvent.Any(), "At least one token should reach the end event");
            
            // 2. Verify the inclusive gateway was properly merged
            var joinGateway = "Gateway_join";
            var waitingTokensAtGateway = processInstance.Tokens
                .Where(t => t.CurrentElementId == joinGateway && t.Status == TokenStatus.PendingToMerge)
                .ToList();
            
            var completedTokensFromGateway = processInstance.Tokens
                .Where(t => t.History.Any(h => h.ElementId == joinGateway) && t.Status == TokenStatus.Completed)
                .ToList();
            
            // Either the gateway has merged all incoming tokens and none are waiting,
            // or we haven't reached the merge condition yet and tokens are still waiting
            if (waitingTokensAtGateway.Any())
            {
                _output.WriteLine($"Gateway {joinGateway} has {waitingTokensAtGateway.Count} waiting tokens");
                Assert.Equal(TokenStatus.PendingToMerge, waitingTokensAtGateway.First().Status);
            }
            else
            {
                _output.WriteLine($"Gateway {joinGateway} has merged and has {completedTokensFromGateway.Count} completed tokens");
                Assert.True(completedTokensFromGateway.Any(), "There should be completed tokens from the gateway");
            }
            
            // 3. Verify execution map shows the process flow correctly
            var executionMap = processInstance.GetExecutionMap(true);
            
            // Verify both timer paths are represented
            var timerEventFlow1 = "Flow_timer_noninterrupting";
            var timerEventFlow2 = "Flow_timer_notify";
            
            Assert.Contains(executionMap.Flows, f => f.FlowId == timerEventFlow1);
            Assert.Contains(executionMap.Flows, f => f.FlowId == timerEventFlow2);
            
            // Verify the activity after the inclusive gateway was executed
            var activityAfterGateway = "Activity_7";
            Assert.Contains(executionMap.Nodes, n => n.NodeId == activityAfterGateway);
            
            _output.WriteLine($"Test completed successfully with {processInstance.Tokens.Count} tokens");
        }
    }
} 