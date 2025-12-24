using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

public interface ITokenExecutionStrategy
{
    /// <summary>برای ترتیب‌دهی (کمتر = زودتر).</summary>
    int Order { get; }

    /// <summary>آیا این Strategy می‌تواند این (token, element) را اجرا کند؟</summary>
    bool CanExecute(Token token, BpmnFlowElement element);

    /// <summary>اجرای منطق همان element روی token/process.</summary>
    Task ExecuteAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct);
}
