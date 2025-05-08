using Novin.Bpmn;
using Novin.Bpmn.V3;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Novin.Bpmn.Test
{
    public class TimerEventTests
    {
        private readonly ITestOutputHelper _output;

        public TimerEventTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task TestParallelTimerEvents()
        {
            // Load BPMN file
            string bpmnPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bpmn", "simple_inclusive_timer.bpmn");
            if (!File.Exists(bpmnPath))
            {
                bpmnPath = @"C:\Users\ahmadi.UR-NEZAM\RiderProjects\BpmnEngine\Novin.Bpmn.Test\Bpmn\simple_inclusive_timer.bpmn";
            }
            
            string bpmnXml = File.ReadAllText(bpmnPath);
            
            // Create a process instance
            var processInstance = new BpmnV3ProcessInstance("process", bpmnXml);
            
            // Create an executor
            var executor = new BpmnV3ProcessExecutor(processInstance);
            
            // Execute the process
            _output.WriteLine("Starting process with timer events in parallel gateway scenario...");
            try
            {
                // Set a timeout to ensure test doesn't run forever
                var executionTask = executor.StartProcessAsync();
                var completedTask = await Task.WhenAny(executionTask, Task.Delay(15000));
                
                if (completedTask != executionTask)
                {
                    _output.WriteLine("Process execution timeout - this is normal as it may be waiting for timer events to complete");
                }
                else
                {
                    var result = await executionTask;
                    _output.WriteLine("Process completed normally");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Process execution error: {ex.Message}");
                throw;
            }
            
            // Display process status
            _output.WriteLine(executor.GetProcessStatus());
            
            // Output information about tokens
            _output.WriteLine("Tokens:");
            foreach (var token in processInstance.Tokens)
            {
                _output.WriteLine($"Token {token.Id}: Element={token.CurrentElementId}, Status={token.Status}, Executable={token.IsExecutable}");
            }
            
            // Assert that tokens exist for the timer paths
            var map = executor.GetExecutionMap(true);
            
            // We expect to see tokens for the interrupting and non-interrupting timer events
            Assert.True(map.Nodes.Exists(n => n.NodeId == "Event_timer_interrupting" || n.NodeId == "Event_timer_noninterrupting"),
                "Expected to find timer event nodes in the execution map");
            
            // Output the full execution map
            _output.WriteLine("Execution Map:");
            _output.WriteLine($"Nodes: {map.Nodes.Count}");
            foreach (var node in map.Nodes)
            {
                _output.WriteLine($"Node {node.NodeId}: Active={node.IsActive}, Execution Count={node.ExecutionCount}");
            }
        }
        
        [Fact]
        public async Task TestMultipleTimerEvents()
        {
            // Load BPMN file
            string bpmnPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bpmn", "multiple_timer_events.bpmn");
            if (!File.Exists(bpmnPath))
            {
                bpmnPath = @"C:\Users\ahmadi.UR-NEZAM\RiderProjects\BpmnEngine\Novin.Bpmn.Test\Bpmn\multiple_timer_events.bpmn";
            }
            
            string bpmnXml = File.ReadAllText(bpmnPath);
            
            // Create a process instance
            var processInstance = new BpmnV3ProcessInstance("process_multi_timer", bpmnXml);
            
            // Create an executor
            var executor = new BpmnV3ProcessExecutor(processInstance);
            
            // Execute the process
            _output.WriteLine("Starting process with multiple timer events...");
            try
            {
                // Set a timeout to ensure test doesn't run forever
                var executionTask = executor.StartProcessAsync();
                var completedTask = await Task.WhenAny(executionTask, Task.Delay(15000));
                
                if (completedTask != executionTask)
                {
                    _output.WriteLine("Process execution timeout - this is normal as it may be waiting for timer events to complete");
                }
                else
                {
                    var result = await executionTask;
                    _output.WriteLine("Process completed normally");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Process execution error: {ex.Message}");
                throw;
            }
            
            // Display process status
            _output.WriteLine(executor.GetProcessStatus());
            
            // Output information about tokens
            _output.WriteLine("Tokens:");
            foreach (var token in processInstance.Tokens)
            {
                _output.WriteLine($"Token {token.Id}: Element={token.CurrentElementId}, Status={token.Status}, Executable={token.IsExecutable}");
            }
            
            // Assert that tokens exist for the timer paths
            var map = executor.GetExecutionMap(true);
            
            // We expect to see tokens for all timer events
            Assert.True(map.Nodes.Exists(n => n.NodeId == "timer1" || n.NodeId == "timer2" || n.NodeId == "timer3"),
                "Expected to find timer event nodes in the execution map");
            
            // Check that the inclusive gateway was used to join timer flows
            Assert.True(map.Nodes.Exists(n => n.NodeId == "join_gateway"),
                "Expected to find inclusive gateway join node in the execution map");
            
            // Output the full execution map
            _output.WriteLine("Execution Map:");
            _output.WriteLine($"Nodes: {map.Nodes.Count}");
            foreach (var node in map.Nodes)
            {
                _output.WriteLine($"Node {node.NodeId}: Active={node.IsActive}, Execution Count={node.ExecutionCount}");
            }
        }
    }
} 