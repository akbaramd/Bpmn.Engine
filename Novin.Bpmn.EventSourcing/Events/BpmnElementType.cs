using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Events
{
    public enum BpmnCategory
    {
        Unknown,
        Event,
        Task,
        Gateway,
        Activity,
        Flow,
        Data
    }

    [JsonConverter(typeof(BpmnElementTypeJsonConverter))]
    public class BpmnElementType : IEquatable<BpmnElementType>
    {
        // Core Properties
        public int Id { get; }
        public string Name { get; }
        public string NameWithNamespace => $"bpmn:{Name}";
        public BpmnCategory Category { get; }

        private BpmnElementType(int id, string name, BpmnCategory category)
        {
            Id = id;
            Name = name;
            Category = category;
        }

        // Static registry
        private static readonly Dictionary<int, BpmnElementType> _byId = new();
        private static readonly Dictionary<string, BpmnElementType> _byName = new(StringComparer.OrdinalIgnoreCase);

        // Factory and registry helper
        private static BpmnElementType Register(int id, string name, BpmnCategory category)
        {
            var type = new BpmnElementType(id, name, category);
            _byId[id] = type;
            _byName[name] = type;
            
            // Also register with namespace prefix for lookup
            _byName[$"bpmn:{name}"] = type;
            
            return type;
        }

        // Static instances
        public static readonly BpmnElementType Unknown = Register(0, "Unknown", BpmnCategory.Unknown);
        public static readonly BpmnElementType StartEvent = Register(1, "StartEvent", BpmnCategory.Event);
        public static readonly BpmnElementType EndEvent = Register(2, "EndEvent", BpmnCategory.Event);
        public static readonly BpmnElementType UserTask = Register(3, "UserTask", BpmnCategory.Task);
        public static readonly BpmnElementType ServiceTask = Register(4, "ServiceTask", BpmnCategory.Task);
        public static readonly BpmnElementType ManualTask = Register(5, "ManualTask", BpmnCategory.Task);
        public static readonly BpmnElementType ScriptTask = Register(6, "ScriptTask", BpmnCategory.Task);
        public static readonly BpmnElementType ExclusiveGateway = Register(7, "ExclusiveGateway", BpmnCategory.Gateway);
        public static readonly BpmnElementType ParallelGateway = Register(8, "ParallelGateway", BpmnCategory.Gateway);
        public static readonly BpmnElementType InclusiveGateway = Register(9, "InclusiveGateway", BpmnCategory.Gateway);
        public static readonly BpmnElementType ComplexGateway = Register(10, "ComplexGateway", BpmnCategory.Gateway);
        public static readonly BpmnElementType SubProcess = Register(11, "SubProcess", BpmnCategory.Activity);
        public static readonly BpmnElementType SequenceFlow = Register(12, "SequenceFlow", BpmnCategory.Flow);
        public static readonly BpmnElementType DataObject = Register(13, "DataObject", BpmnCategory.Data);
        public static readonly BpmnElementType DataObjectReference = Register(14, "DataObjectReference", BpmnCategory.Data);
        public static readonly BpmnElementType DataStore = Register(15, "DataStore", BpmnCategory.Data);
        public static readonly BpmnElementType DataStoreReference = Register(16, "DataStoreReference", BpmnCategory.Data);
        public static readonly BpmnElementType DataInput = Register(17, "DataInput", BpmnCategory.Data);
        public static readonly BpmnElementType DataOutput = Register(18, "DataOutput", BpmnCategory.Data);
        public static readonly BpmnElementType DataInputAssociation = Register(19, "DataInputAssociation", BpmnCategory.Data);
        public static readonly BpmnElementType DataOutputAssociation = Register(17, "DataOutputAssociation", BpmnCategory.Data);
        public static readonly BpmnElementType DataInputOutput = Register(20, "DataInputOutput", BpmnCategory.Data);
        public static readonly BpmnElementType DataInputOutputAssociation = Register(21, "DataInputOutputAssociation", BpmnCategory.Data);
        public static readonly BpmnElementType BusinessRuleTask = Register(22, "BusinessRuleTask", BpmnCategory.Task);
        public static readonly BpmnElementType SendTask = Register(23, "SendTask", BpmnCategory.Task);
        public static readonly BpmnElementType ReceiveTask = Register(24, "ReceiveTask", BpmnCategory.Task);
        public static readonly BpmnElementType Task = Register(25, "Task", BpmnCategory.Task);
        public static readonly BpmnElementType EventBasedGateway = Register(26, "EventBasedGateway", BpmnCategory.Gateway);
     
        public static BpmnElementType FromId(int id) => _byId.TryGetValue(id, out var type) ? type : Unknown;
        
        public static BpmnElementType FromName(string name)
        {
            // Allow lookup with or without namespace prefix
            return _byName.TryGetValue(name, out var type) ? type : Unknown;
        }
        
        /// <summary>
        /// Get BPMN element type from XML local name (with or without namespace)
        /// </summary>
        public static BpmnElementType FromXmlName(string xmlName)
        {
            if (string.IsNullOrEmpty(xmlName))
                return Unknown;
                
            // Handle full namespace formats like {http://www.omg.org/spec/BPMN/20100524/MODEL}serviceTask
            int colonIndex = xmlName.LastIndexOf(':');
            int braceIndex = xmlName.LastIndexOf('}');
            
            string localName;
            if (braceIndex > 0 && braceIndex < xmlName.Length - 1)
            {
                // Extract local name from namespace format {ns}localName
                localName = xmlName.Substring(braceIndex + 1);
            }
            else if (colonIndex > 0 && colonIndex < xmlName.Length - 1)
            {
                // Extract local name from prefix:localName format
                localName = xmlName.Substring(colonIndex + 1);
            }
            else
            {
                // Use as-is if no namespace or prefix
                localName = xmlName;
            }
            
            // Return the type based on the local name
            return FromName(localName);
        }
        
        public override string ToString() => NameWithNamespace;

        public bool Equals(BpmnElementType? other) => other != null && Id == other.Id;
        public override bool Equals(object? obj) => obj is BpmnElementType other && Equals(other);
        public override int GetHashCode() => Id;

        public static bool operator ==(BpmnElementType? a, BpmnElementType? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(BpmnElementType? a, BpmnElementType? b) => !(a == b);
    }

    public class BpmnElementTypeJsonConverter : JsonConverter<BpmnElementType>
    {
        public override BpmnElementType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var name = reader.GetString();
            return BpmnElementType.FromName(name ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, BpmnElementType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.NameWithNamespace);
        }
    }
}
