using System.Xml;
using System.Xml.Serialization;
using Novin.Bpmn.Models.Models;

public interface IBonyanIoAccessor
{
    bool TryGetIoMapping(BpmnFlowElement element, out BonyanIoMapping? mapping);
    bool TryGetIo(BpmnFlowElement element, out BonyanIo? io);
}

public sealed class BonyanIoAccessor : IBonyanIoAccessor
{
    private static readonly XmlSerializer IoMappingSerializer = new(typeof(BonyanIoMapping));
    private static readonly XmlSerializer IoSerializer = new(typeof(BonyanIo));

    public bool TryGetIoMapping(BpmnFlowElement element, out BonyanIoMapping? mapping)
    {
        mapping = null;
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

    private static IEnumerable<object> GetExtensionItems(BpmnFlowElement element)
    {
        // Robust reflection: extensionElements / ExtensionElements + Any
        var t = element.GetType();
        var ext = t.GetProperty("extensionElements")?.GetValue(element)
               ?? t.GetProperty("ExtensionElements")?.GetValue(element);

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
