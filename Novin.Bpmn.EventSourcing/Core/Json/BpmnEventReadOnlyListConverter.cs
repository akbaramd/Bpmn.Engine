using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Json
{
    /// <summary>
    /// Specialized JSON converter for handling read-only lists of IBpmnEvent objects.
    /// Used for the History property in ProcessInstanceState.
    /// </summary>
    public class BpmnEventReadOnlyListConverter : JsonConverter<IReadOnlyList<IBpmnEvent>>
    {
        public override IReadOnlyList<IBpmnEvent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Create a new options instance without this converter to avoid infinite recursion
            var nestedOptions = new JsonSerializerOptions(options);
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is BpmnEventReadOnlyListConverter)
                {
                    nestedOptions.Converters.Remove(converter);
                    break;
                }
            }

            // Use the regular list converter
            var listConverter = new BpmnEventListConverter();
            var mutableList = listConverter.Read(ref reader, typeof(List<IBpmnEvent>), nestedOptions);
            
            // Return as read-only list
            return mutableList.AsReadOnly();
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlyList<IBpmnEvent> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            // Create a new options instance without this converter to avoid infinite recursion
            var nestedOptions = new JsonSerializerOptions(options);
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is BpmnEventReadOnlyListConverter)
                {
                    nestedOptions.Converters.Remove(converter);
                    break;
                }
            }

            // Use the regular list converter to write the list
            var listConverter = new BpmnEventListConverter();
            var mutableList = new List<IBpmnEvent>(value);
            listConverter.Write(writer, mutableList, nestedOptions);
        }
    }
} 