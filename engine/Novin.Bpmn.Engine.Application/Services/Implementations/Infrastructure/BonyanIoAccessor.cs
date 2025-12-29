using System.Xml;
using System.Xml.Serialization;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class BonyanIoAccessor : IBonyanIoAccessor
{
    private static readonly XmlSerializer IoMappingSerializer = new(typeof(BonyanIoMapping));
    private static readonly XmlSerializer IoSerializer = new(typeof(BonyanIo));

    public bool TryGetIoMapping(BpmnFlowElement element, out BonyanIoMapping? mapping)
    {
        mapping = null;
        
        // First, try to get extensionElements directly
        var ext = GetExtensionElements(element);
        if (ext?.BonyanIoMapping != null)
        {
            mapping = ext.BonyanIoMapping;
            return true;
        }
        
        // Fallback: parse from Any array (for backward compatibility or when deserialized as XmlElement)
        foreach (var obj in GetExtensionItems(element))
        {
            if (obj is BonyanIoMapping m) { mapping = m; return true; }

            if (obj is XmlElement xe &&
                xe.LocalName == "ioMapping" &&
                xe.NamespaceURI == BpmnXmlNamespaces.Bonyan)
            {
                using var r = new XmlNodeReader(xe);
                mapping = (BonyanIoMapping)IoMappingSerializer.Deserialize(r)!;
                return true;
            }
        }
        return false;
    }

    public bool TryGetIo(BpmnFlowElement element, out BonyanIo? io)
    {
        io = null;
        
        // First, try to get extensionElements directly
        var ext = GetExtensionElements(element);
        if (ext?.BonyanIo != null)
        {
            io = ext.BonyanIo;
            return true;
        }
        
        // Fallback: parse from Any array (for backward compatibility or when deserialized as XmlElement)
        foreach (var obj in GetExtensionItems(element))
        {
            if (obj is BonyanIo i) { io = i; return true; }

            if (obj is XmlElement xe &&
                xe.LocalName == "io" &&
                xe.NamespaceURI == BpmnXmlNamespaces.Bonyan)
            {
                using var r = new XmlNodeReader(xe);
                io = (BonyanIo)IoSerializer.Deserialize(r)!;
                return true;
            }
        }
        return false;
    }

    private static BpmnExtensionElements? GetExtensionElements(BpmnFlowElement element)
    {
        var t = element.GetType();
        var ext = t.GetProperty("extensionElements")?.GetValue(element)
                  ?? t.GetProperty("ExtensionElements")?.GetValue(element);
        return ext as BpmnExtensionElements;
    }

    private static IEnumerable<object> GetExtensionItems(BpmnFlowElement element)
    {
        // Robust reflection: extensionElements / ExtensionElements + Any
        var ext = GetExtensionElements(element);
        if (ext == null) yield break;

        var extType = ext.GetType();
        var any = extType.GetProperty("Any")?.GetValue(ext)
                  ?? extType.GetProperty("any")?.GetValue(ext);

        if (any is object[] arr)
        {
            foreach (var x in arr) if (x != null) yield return x;
            yield break;
        }

        if (any is System.Collections.IEnumerable en)
        {
            foreach (var x in en) if (x != null) yield return x;
        }
    }
}