﻿namespace Novin.Bpmn.Abstractions;

public interface IServiceTaskHandler
{
    public Task HandleAsync(BpmnState state);
}