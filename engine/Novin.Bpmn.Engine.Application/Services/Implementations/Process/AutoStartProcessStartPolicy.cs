using Microsoft.Extensions.Options;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class AutoStartProcessStartPolicy : IProcessStartPolicy
{
    private readonly ProcessRuntimeOptions _options;

    public AutoStartProcessStartPolicy(IOptions<ProcessRuntimeOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public bool ShouldAutoStart(Process process)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        return _options.AutoStartOnCreate;
    }
}

