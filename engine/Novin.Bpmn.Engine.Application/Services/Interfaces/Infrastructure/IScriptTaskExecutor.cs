using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

// =============================

public interface IScriptTaskExecutor
{
    Task ExecuteAsync(Process process, Token token, BpmnScriptTask task, CancellationToken ct);
}

