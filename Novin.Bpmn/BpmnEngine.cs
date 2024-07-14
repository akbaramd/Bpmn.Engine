using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test
{
    public class BpmnEngine
    {
        private readonly ITaskExecutor _scriptTaskExecutor;
        private readonly IGatewayExecutor _exclusiveGatewayExecutor;
        private readonly IGatewayExecutor _inclusiveGatewayExecutor;
        private readonly IUserTaskExecutor _userTaskExecutor;

        public BpmnEngine(string filePath) : this(filePath, new BpmnInstance())
        {
            
            Instance.CurrentNodeId = FindStartNode()?.id;
        }

        public BpmnEngine(string filePath, BpmnInstance? context)
        {
            Instance = context ?? new BpmnInstance();
            Instance.BpmnDefinitions = DeserializeBpmnFile(filePath);
            _scriptTaskExecutor = new ScriptTaskExecutor();
            _exclusiveGatewayExecutor = new ExclusiveGatewayExecutor();
            _inclusiveGatewayExecutor = new InclusiveGatewayExecutor();
            _userTaskExecutor = new UserTaskExecutor();
        }

        public BpmnInstance Instance { get; }

        private BpmnDefinitions DeserializeBpmnFile(string filePath)
        {
            var xmlNamespaces = new XmlSerializerNamespaces();
            xmlNamespaces.Add("bpmn", "http://www.omg.org/spec/BPMN/20100524/MODEL");
            xmlNamespaces.Add("bpmndi", "http://www.omg.org/spec/BPMN/20100524/DI");
            xmlNamespaces.Add("dc", "http://www.omg.org/spec/DD/20100524/DC");
            xmlNamespaces.Add("di", "http://www.omg.org/spec/DD/20100524/DI");
            xmlNamespaces.Add("camunda", "http://camunda.org/schema/1.0/bpmn");
            xmlNamespaces.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

            var xmlContent = File.ReadAllText(filePath);

            var serializer = new XmlSerializer(typeof(BpmnDefinitions), "http://www.omg.org/spec/BPMN/20100524/MODEL");
            using var stringReader = new StringReader(xmlContent);
            return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
        }

        public BpmnStartEvent? FindStartNode()
        {
            foreach (var item in Instance.BpmnDefinitions.Items)
                if (item is BpmnProcess process)
                    foreach (var element in process.Items)
                        if (element is BpmnStartEvent startEvent)
                            return startEvent;
            return null;
        }

        public BpmnEndEvent? FindLastNode()
        {
            foreach (var item in Instance.BpmnDefinitions.Items)
                if (item is BpmnProcess process)
                    foreach (var element in process.Items)
                        if (element is BpmnEndEvent endEvent)
                            return endEvent;
            return null;
        }

        public BpmnFlowElement? FindNextNode(string currentNodeId)
        {
            foreach (var item in Instance.BpmnDefinitions.Items)
                if (item is BpmnProcess process)
                    foreach (var element in process.Items)
                        if (element is BpmnSequenceFlow sequenceFlow && sequenceFlow.sourceRef == currentNodeId)
                            return process.Items.FirstOrDefault(e => e?.id == sequenceFlow.targetRef);
            return null;
        }

        public BpmnFlowElement? FindPreviousNode(string currentNodeId)
        {
            foreach (var item in Instance.BpmnDefinitions.Items)
                if (item is BpmnProcess process)
                    foreach (var element in process.Items)
                        if (element is BpmnSequenceFlow sequenceFlow && sequenceFlow.targetRef == currentNodeId)
                        {
                            var sourceElement = process.Items.FirstOrDefault(e => e?.id == sequenceFlow.sourceRef);
                            if (sourceElement is BpmnExclusiveGateway || sourceElement is BpmnInclusiveGateway || sourceElement is BpmnParallelGateway)
                            {
                                // Handle gateway specific logic to find the best route
                                var previousNode = FindPreviousNode(sourceElement.id);
                                return previousNode;
                            }
                            return sourceElement;
                        }
            return null;
        }

        public BpmnInstance ExecuteProcess()
        {
            while (true)
            {
                if (Instance.CurrentNodeId == null)
                {
                    var startNode = FindStartNode();
                    if (startNode != null)
                    {
                        Instance.CurrentNodeId = startNode.id;
                    }
                    else
                    {
                        return Instance;
                    }
                }

                ExecuteNext();

                if (Instance.IsPaused || Instance.PendingUserTask != null || Instance.CurrentNodeId == null)
                {
                    break;
                }
            }

            return Instance;
        }

        public BpmnInstance ExecuteNext()
        {
            if (Instance.CurrentNodeId == null)
            {
                return Instance;
            }

            var currentNodeId = Instance.CurrentNodeId;
            Instance.History.Push(currentNodeId); // Add to history for rollback

            var currentNode = FindCurrentNode(currentNodeId);
            switch (currentNode)
            {
                case BpmnStartEvent scriptTask:
                    Instance.CurrentNodeId = FindNextNodeId(currentNodeId);
                    break;
                case BpmnScriptTask scriptTask:
                    _scriptTaskExecutor.ExecuteAsync(scriptTask, Instance).Wait();
                    Instance.CurrentNodeId = FindNextNodeId(currentNodeId);
                    break;
                case BpmnExclusiveGateway exclusiveGateway:
                    var flow =(BpmnSequenceFlow?) _exclusiveGatewayExecutor.Execute(exclusiveGateway, Instance);
                    Instance.CurrentNodeId = flow?.targetRef;
                    break;
                case BpmnInclusiveGateway inclusiveGateway:
                    var inclusiveFlow =(BpmnSequenceFlow?) _inclusiveGatewayExecutor.Execute(inclusiveGateway, Instance);
                    Instance.CurrentNodeId = inclusiveFlow?.targetRef;
                    break;
                case BpmnParallelGateway parallelGateway:
                    // Handle parallel gateway by executing all outgoing flows
                    foreach (var sequenceFlow in Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                        .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                        .Where(flow => flow.sourceRef == parallelGateway.id))
                    {
                        ExecuteNext();
                    }
                    return Instance; // Parallel execution completed
                case BpmnUserTask userTask:
                    _userTaskExecutor.Execute(userTask, Instance);
                    Instance.CurrentNodeId = FindNextNodeId(currentNodeId);
                    return Instance; // Exit the method to wait for user interaction
                case BpmnEndEvent:
                    Console.WriteLine("Process completed.");
                    Instance.CurrentNodeId = null; // Mark process as completed
                    return Instance;
            }


            return Instance;
        }

        private string? FindNextNodeId(string currentNodeId)
        {
            var nextNode = FindNextNode(currentNodeId);
            return nextNode?.id;
        }

        private BpmnFlowElement? FindCurrentNode(string currentNodeId)
        {
            foreach (var item in Instance.BpmnDefinitions.Items)
                if (item is BpmnProcess process)
                    foreach (var element in process.Items)
                        if (element.id == currentNodeId)
                            return element;
            return null;
        }

        public void CompleteUserTask(string userId, string taskId)
        {
            var userTask = (BpmnUserTask?)Instance.PendingUserTask;
            if (userTask != null && userTask.id == taskId)
            {
                Console.WriteLine($"User Task '{taskId}' completed by user '{userId}'.");
                Instance.PendingUserTask = null;
                ExecuteNext();
            }
            else
            {
                Console.WriteLine("User does not have permission to complete this task.");
            }
        }

        public void Rollback()
        {
            if (Instance.History.Count > 1)
            {
                Instance.History.Pop(); // Remove current node
                var previousNodeId = Instance.History.Peek(); // Get previous node
                Instance.CurrentNodeId = previousNodeId;
                Console.WriteLine($"Rolling back to node '{previousNodeId}'.");
            }
            else
            {
                Console.WriteLine("No previous node to rollback to.");
            }
        }
    }

    public class ScriptGlobals
    {
        public BpmnInstance Instance { get; set; } = new BpmnInstance();
    }
}
