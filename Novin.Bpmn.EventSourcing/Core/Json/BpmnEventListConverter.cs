using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Json
{
    /// <summary>
    /// Specialized JSON converter for handling lists of IBpmnEvent objects.
    /// This ensures proper polymorphic serialization and deserialization of event collections.
    /// </summary>
    public class BpmnEventListConverter : JsonConverter<List<IBpmnEvent>>
    {
        private const string TypeDiscriminatorPropertyName = "$type";

        public override List<IBpmnEvent> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Expected StartArray token, got {reader.TokenType}");
            }

            var events = new List<IBpmnEvent>();
            
            // Create a new options instance without this converter to avoid infinite recursion
            var nestedOptions = new JsonSerializerOptions(options);
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is BpmnEventListConverter)
                {
                    nestedOptions.Converters.Remove(converter);
                    break;
                }
            }

            // Ensure we have the polymorphic converter
            var hasPolymorphicConverter = false;
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is PolymorphicJsonConverter<IBpmnEvent>)
                {
                    hasPolymorphicConverter = true;
                    break;
                }
            }
            
            if (!hasPolymorphicConverter)
            {
                nestedOptions.Converters.Add(new PolymorphicJsonConverter<IBpmnEvent>());
            }

            // Read each event in the array
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return events;
                }

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    try 
                    {
                        // First try to parse using our polymorphic converter
                        var eventObj = JsonSerializer.Deserialize<IBpmnEvent>(ref reader, nestedOptions);
                        if (eventObj != null)
                        {
                            events.Add(eventObj);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If that fails, at least skip this object to continue parsing
                        Console.WriteLine($"Error deserializing event: {ex.Message}");
                        SkipToEndObject(ref reader);
                    }
                }
                else if (reader.TokenType == JsonTokenType.Null)
                {
                    // Skip null entries
                    continue;
                }
                else
                {
                    throw new JsonException($"Unexpected token {reader.TokenType} in event array");
                }
            }

            throw new JsonException("Unexpected end of JSON while parsing event array");
        }

        public override void Write(Utf8JsonWriter writer, List<IBpmnEvent> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            
            // Create options without this converter to avoid infinite recursion
            var nestedOptions = new JsonSerializerOptions(options);
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is BpmnEventListConverter)
                {
                    nestedOptions.Converters.Remove(converter);
                    break;
                }
            }
            
            // Ensure we have the polymorphic converter
            var hasPolymorphicConverter = false;
            foreach (var converter in nestedOptions.Converters)
            {
                if (converter is PolymorphicJsonConverter<IBpmnEvent>)
                {
                    hasPolymorphicConverter = true;
                    break;
                }
            }
            
            if (!hasPolymorphicConverter)
            {
                nestedOptions.Converters.Add(new PolymorphicJsonConverter<IBpmnEvent>());
            }

            // Write each event in the array
            foreach (var evt in value)
            {
                if (evt == null)
                {
                    writer.WriteNullValue();
                    continue;
                }
                
                try
                {
                    JsonSerializer.Serialize(writer, evt, evt.GetType(), nestedOptions);
                }
                catch (Exception ex)
                {
                    // If there's an error, write a placeholder object with error info
                    writer.WriteStartObject();
                    writer.WriteString("$error", $"Failed to serialize event: {ex.Message}");
                    writer.WriteString("EventType", evt.EventType);
                    writer.WriteString("EventId", evt.EventId.ToString());
                    writer.WriteEndObject();
                }
            }
            
            writer.WriteEndArray();
        }

        /// <summary>
        /// Helper method to skip to the end of the current object when there's an error
        /// </summary>
        private static void SkipToEndObject(ref Utf8JsonReader reader)
        {
            int depth = 1;
            while (depth > 0 && reader.Read())
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    depth++;
                }
                else if (reader.TokenType == JsonTokenType.EndObject)
                {
                    depth--;
                }
            }
        }
    }
} 