﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Models;

public class BpmnEngineTest
{
    private readonly string _bpmnFilePath = "C:\\Users\\ahmadi.UR-NEZAM\\RiderProjects\\BpmnEngine\\Novin.Bpmn.Test\\Bpmn\\simple_inclusive.bpmn";

    [Fact]
    public async Task TestBpmnEngineStepByStepExecution()
    {
        // Create an instance of the BPMN engine with the given file path and dependencies
        var engine = new BpmnEngine(_bpmnFilePath);

        // Execute the process step-by-step
        await engine.StartProcess(false);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);
        Assert.Single(engine.State.NodeQueue);

        await engine.StartProcess(false);
        Assert.Equal(3,engine.State.NodeQueue.Count);
        
        await engine.StartProcess(false);
        Assert.Equal(2,engine.State.NodeQueue.Count(x => x.Instances.First().IsExecutable));
    }

}