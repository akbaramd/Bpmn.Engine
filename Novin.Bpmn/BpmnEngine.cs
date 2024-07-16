using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test
{
    public class BpmnEngine
    {
        private readonly ITaskExecutor _scriptTaskExecutor;
        public readonly IUserTaskExecutor _userTaskExecutor;
        private readonly IStartEventExecutor _startEventExecutor;
        private readonly IEndEventExecutor _endEventExecutor;

        public BpmnInstance Instance { get; }
        public BpmnRoute Route { get; }

        public BpmnEngine(string filePath)
        {
            try
            {
                Instance = new BpmnInstance();
                var converter = new BpmnConverter();
                var definitions = DeserializeBpmnFile(filePath);
                Route = converter.Convert(definitions);

                _scriptTaskExecutor = new ScriptTaskExecutor();
                _userTaskExecutor = new UserTaskExecutor();
                _startEventExecutor = new StartEventExecutor();
                _endEventExecutor = new EndEventExecutor();

                Instance.ActiveRoutes.Add(Route);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private BpmnDefinitions DeserializeBpmnFile(string filePath)
        {
            var xmlContent = System.IO.File.ReadAllText(filePath);
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions), "http://www.omg.org/spec/BPMN/20100524/MODEL");
            using var stringReader = new System.IO.StringReader(xmlContent);
            return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
        }

        public async Task<BpmnInstance> ExecuteProcessAsync()
        {
            while (Instance.ActiveRoutes.Any(route => !route.Executed))
            {
                var tasks = Instance.ActiveRoutes.Where(route => !route.Executed).Select(ExecuteAsync).ToList();
                await Task.WhenAll(tasks);
            }
            return Instance;
        }

        private async Task ExecuteAsync(BpmnRoute currentRoute)
        {
            if (currentRoute == null || currentRoute.Executed)
            {
                return;
            }

            if (currentRoute.Element != null)
            {
                switch (currentRoute.Element)
                {
                    case BpmnStartEvent startEvent:
                        await _startEventExecutor.ExecuteAsync(startEvent, this);
                        break;
                    case BpmnScriptTask scriptTask:
                        await _scriptTaskExecutor.ExecuteAsync(scriptTask, this);
                        break;
                    case BpmnUserTask userTask:
                        await _userTaskExecutor.ExecuteAsync(userTask, this);
                        break;
                    case BpmnEndEvent endEvent:
                        await _endEventExecutor.ExecuteAsync(endEvent, this);
                        break;
                    case BpmnParallelGateway parallelGateway:
                        // Check if all incoming branches are executed
                        if (!AreAllIncomingBranchesExecuted(currentRoute))
                        {
                            return; // Wait until all incoming branches are executed
                        }
                        break;
                }
                
                currentRoute.Executed = true;
                Instance.History.Push(currentRoute);
            }

            Instance.ActiveRoutes.Remove(currentRoute);

            var nextRoutes = await FindNextRoutes(currentRoute);
            Instance.ActiveRoutes.AddRange(nextRoutes);
        }

        private async Task<List<BpmnRoute>> FindNextRoutes(BpmnRoute currentRoute)
        {
            var nextRoutes = new List<BpmnRoute>();

            if (currentRoute.Element is BpmnGateway gateway)
            {
                var gatewayNextRoutes = await FindNextRoute(currentRoute);
                nextRoutes.AddRange(gatewayNextRoutes);
            }
            else
            {
                nextRoutes.AddRange(currentRoute.NextSteps);
            }

            return nextRoutes;
        }

        private async Task<List<BpmnRoute>> FindNextRoute(BpmnRoute route)
        {
            var nextRoutes = new List<BpmnRoute>();

            switch (route.Element)
            {
                case BpmnExclusiveGateway exclusiveGateway:
                    nextRoutes = await EvaluateExclusiveGateway(route);
                    break;
                case BpmnInclusiveGateway inclusiveGateway:
                    nextRoutes = await EvaluateInclusiveGateway(route);
                    break;
                case BpmnParallelGateway parallelGateway:
                    nextRoutes = await EvaluateParallelGateway(route);
                    break;
            }

            return nextRoutes;
        }

        private async Task<List<BpmnRoute>> EvaluateExclusiveGateway(BpmnRoute route)
        {
            foreach (var nextRoute in route.Outgoing)
            {
                if (await EvaluateCondition(nextRoute.conditionExpression))
                {
                    return route.NextSteps.Where(x => x.Id.Equals(nextRoute.targetRef)).ToList();
                }
            }

            return new List<BpmnRoute>();
        }

        private async Task<List<BpmnRoute>> EvaluateInclusiveGateway(BpmnRoute route)
        {
            var nextRoutes = new List<BpmnRoute>();
            foreach (var nextRoute in route.Outgoing)
            {
                if (await EvaluateCondition(nextRoute.conditionExpression))
                {
                    nextRoutes.AddRange(route.NextSteps.Where(x => x.Id.Equals(nextRoute.targetRef)).ToList());
                }
            }

            return nextRoutes;
        }

        private async Task<List<BpmnRoute>> EvaluateParallelGateway(BpmnRoute route)
        {
            // Proceed with the outgoing branches of the parallel gateway
            return route.NextSteps.ToList();
        }

        private bool AreAllIncomingBranchesExecuted(BpmnRoute route)
        {
            return route.Incoming.All(incoming =>
                Instance.History.Any(historyRoute => historyRoute.Id == incoming.sourceRef));
        }

        private async Task<bool> EvaluateCondition(BpmnExpression? conditionExpression)
        {
            try
            {
                if (conditionExpression != null)
                {
                    var expression = string.Join(" ", conditionExpression.Text);
                    var globals = new ScriptGlobals { Instance = Instance };
                    var scriptHandler = new ScriptHandler();
                    return await scriptHandler.EvaluateConditionAsync(expression, globals);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error evaluating condition: {ex.Message}");
                return false;
            }

            return false;
        }
    }

    public class ScriptGlobals
    {
        public BpmnInstance Instance { get; set; } = new BpmnInstance();
    }
}
