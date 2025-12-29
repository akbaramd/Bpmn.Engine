using System.Xml;
using System.Xml.Serialization;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public interface IBonyanIoAccessor
{
    bool TryGetIoMapping(BpmnFlowElement element, out BonyanIoMapping? mapping);
    bool TryGetIo(BpmnFlowElement element, out BonyanIo? io);
}

