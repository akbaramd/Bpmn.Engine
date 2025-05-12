using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Events
{
    /// <summary>
    /// Categories of BPMN element types
    /// </summary>
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

    /// <summary>
    /// DDD-style value object representing BPMN element types
    /// </summary>
    [JsonConverter(typeof(BpmnElementTypeJsonConverter))]
    public class BpmnElementType : IEquatable<BpmnElementType>
    {
        // Static predefined instances
        public static readonly BpmnElementType Unknown            = new(0,  "Unknown",            BpmnCategory.Unknown);
        public static readonly BpmnElementType StartEvent         = new(1,  "StartEvent",         BpmnCategory.Event,   "start", "startevent");
        public static readonly BpmnElementType EndEvent           = new(2,  "EndEvent",           BpmnCategory.Event,   "end",   "endevent");
        public static readonly BpmnElementType IntermediateCatchEvent  = new(17, "IntermediateCatchEvent",  BpmnCategory.Event);
        public static readonly BpmnElementType IntermediateThrowEvent  = new(18, "IntermediateThrowEvent",  BpmnCategory.Event);
        public static readonly BpmnElementType BoundaryEvent      = new(19, "BoundaryEvent",      BpmnCategory.Event);

        public static readonly BpmnElementType UserTask           = new(3,  "UserTask",           BpmnCategory.Task,    "user");
        public static readonly BpmnElementType ServiceTask        = new(4,  "ServiceTask",        BpmnCategory.Task,    "service");
        public static readonly BpmnElementType ScriptTask         = new(5,  "ScriptTask",         BpmnCategory.Task,    "script");
        public static readonly BpmnElementType BusinessRuleTask   = new(6,  "BusinessRuleTask",   BpmnCategory.Task);
        public static readonly BpmnElementType ManualTask         = new(7,  "ManualTask",         BpmnCategory.Task);
        public static readonly BpmnElementType ReceiveTask        = new(8,  "ReceiveTask",        BpmnCategory.Task);
        public static readonly BpmnElementType SendTask           = new(9,  "SendTask",           BpmnCategory.Task);
        public static readonly BpmnElementType Task               = new(20, "Task",               BpmnCategory.Task);

        public static readonly BpmnElementType ExclusiveGateway   = new(10, "ExclusiveGateway",   BpmnCategory.Gateway, "xor", "excgateway");
        public static readonly BpmnElementType ParallelGateway    = new(11, "ParallelGateway",    BpmnCategory.Gateway, "and", "pargateway");
        public static readonly BpmnElementType InclusiveGateway   = new(12, "InclusiveGateway",   BpmnCategory.Gateway, "or",  "inclgateway");
        public static readonly BpmnElementType ComplexGateway     = new(13, "ComplexGateway",     BpmnCategory.Gateway);
        public static readonly BpmnElementType EventBasedGateway  = new(14, "EventBasedGateway",  BpmnCategory.Gateway);

        public static readonly BpmnElementType SubProcess         = new(15, "SubProcess",         BpmnCategory.Activity);
        public static readonly BpmnElementType CallActivity       = new(16, "CallActivity",       BpmnCategory.Activity);

        public static readonly BpmnElementType SequenceFlow       = new(21, "SequenceFlow",       BpmnCategory.Flow);
        public static readonly BpmnElementType MessageFlow        = new(22, "MessageFlow",        BpmnCategory.Flow);
        public static readonly BpmnElementType Association        = new(23, "Association",        BpmnCategory.Flow);

        public static readonly BpmnElementType DataObject         = new(24, "DataObject",         BpmnCategory.Data);
        public static readonly BpmnElementType DataStore          = new(25, "DataStore",          BpmnCategory.Data);

        // Thread-safe registries
        private static readonly object _lock = new();
        private static readonly Dictionary<int, BpmnElementType>    _idRegistry   = new();
        private static readonly Dictionary<string, BpmnElementType> _nameRegistry = new(StringComparer.OrdinalIgnoreCase);

        // All aliases map back to their instance
        private static readonly Dictionary<string, BpmnElementType> _aliasRegistry = new(StringComparer.OrdinalIgnoreCase);

        // Instance props
        public int Id                { get; }
        public string Name           { get; }
        public BpmnCategory Category { get; }
        public IReadOnlyList<string> Aliases { get; }

        // Static ctor to register built-ins
        static BpmnElementType() => RegisterAll(typeof(BpmnElementType));

        // Private to control instantiation
        private BpmnElementType(int id, string name, BpmnCategory category, params string[] aliases)
        {
            Id       = id;
            Name     = name;
            Category = category;
            Aliases  = aliases?.ToList() ?? new List<string>();
            RegisterInstance(this);
        }

        /// <summary>Register a single instance and its aliases</summary>
        private static void RegisterInstance(BpmnElementType type)
        {
            lock (_lock)
            {
                _idRegistry[type.Id]     = type;
                _nameRegistry[type.Name] = type;
                foreach (var alias in type.Aliases)
                {
                    var key = Canonicalize(alias);
                    _aliasRegistry[key] = type;
                }
            }
        }

        /// <summary>Scan all public static fields of given type</summary>
        private static void RegisterAll(Type t)
        {
            var fields = t.GetFields(System.Reflection.BindingFlags.Public |
                                     System.Reflection.BindingFlags.Static |
                                     System.Reflection.BindingFlags.DeclaredOnly)
                          .Where(f => f.FieldType == typeof(BpmnElementType));

            foreach (var f in fields)
                RegisterInstance((BpmnElementType)f.GetValue(null)!);
        }

        /// <summary>Canonical form: lowercase, no spaces/dashes/underscores</summary>
        private static string Canonicalize(string s) =>
            s?.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant() ?? string.Empty;

        /// <summary>Lookup by ID</summary>
        public static BpmnElementType FromId(int id) =>
            _idRegistry.TryGetValue(id, out var t) ? t : Unknown;

        /// <summary>Exact name match (ignores "bpmn:" prefix)</summary>
        public static BpmnElementType FromExactName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Unknown;
            var n = name.StartsWith("bpmn:", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(5)
                : name;
            return _nameRegistry.TryGetValue(n, out var t) ? t : Unknown;
        }

        /// <summary>
        /// Flexible parse: exact → alias → fallback Unknown
        /// </summary>
        public static BpmnElementType FromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Unknown;

            // strip prefix
            var trimmed = input.StartsWith("bpmn:", StringComparison.OrdinalIgnoreCase)
                ? input.Substring(5)
                : input;

            // 1. exact name
            var exact = FromExactName(trimmed);
            if (exact != Unknown)
                return exact;

            // 2. alias
            var key = Canonicalize(trimmed);
            if (_aliasRegistry.TryGetValue(key, out var aliased))
                return aliased;

            // 3. Unknown
            return Unknown;
        }

        /// <summary>Throw if unrecognized (and non-empty)</summary>
        public static BpmnElementType Parse(string s)
        {
            var t = FromString(s);
            if (t == Unknown && !string.IsNullOrWhiteSpace(s) && !s.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Could not parse '{s}' as BpmnElementType", nameof(s));
            return t;
        }

        /// <summary>Safe try-parse</summary>
        public static bool TryParse(string s, out BpmnElementType result)
        {
            result = FromString(s);
            return result != Unknown || string.IsNullOrWhiteSpace(s) || s.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Get all registered types</summary>
        public static IEnumerable<BpmnElementType> GetAll() => _idRegistry.Values;

        /// <summary>XML rep (lowercases Unknown)</summary>
        public string ToXmlString() =>
            this == Unknown ? "unknown" : $"bpmn:{Name}";

        /// <summary>Category check</summary>
        public bool IsOfCategory(BpmnCategory cat) => Category == cat;

        // Value-object equality
        public bool Equals(BpmnElementType? other) =>
            other is not null && Id == other.Id;

        public override bool Equals(object? obj) =>
            obj is BpmnElementType t && Equals(t);

        public override int GetHashCode() => Id;

        public static bool operator ==(BpmnElementType? a, BpmnElementType? b) =>
            a?.Equals(b) ?? b is null;

        public static bool operator !=(BpmnElementType? a, BpmnElementType? b) => !(a == b);

        public static implicit operator BpmnElementType(int id) => FromId(id);
        public override string ToString() => Name;
        public static implicit operator string(BpmnElementType t) => t?.Name ?? "";
    }

    /// <summary>
    /// JSON converter for BpmnElementType using its Name
    /// </summary>
    public class BpmnElementTypeJsonConverter : JsonConverter<BpmnElementType>
    {
        public override BpmnElementType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            BpmnElementType.FromString(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, BpmnElementType value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Name);
    }
}
