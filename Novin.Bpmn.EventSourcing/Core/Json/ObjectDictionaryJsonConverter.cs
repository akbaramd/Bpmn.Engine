using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Core.Json
{
    public class ObjectDictionaryJsonConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(
            ref Utf8JsonReader reader, 
            Type typeToConvert, 
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject token, got {reader.TokenType}");
            }

            var dictionary = new Dictionary<string, object>();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException($"Expected PropertyName token, got {reader.TokenType}");
                }

                var propertyName = reader.GetString();
                
                reader.Read();
                dictionary.Add(propertyName, ReadValue(ref reader, options));
            }

            return dictionary;
        }

        public override void Write(
            Utf8JsonWriter writer, 
            Dictionary<string, object> value, 
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options);
            }
            
            writer.WriteEndObject();
        }

        private object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int intValue))
                        return intValue;
                    if (reader.TryGetInt64(out long longValue))
                        return longValue;
                    return reader.GetDouble();
                case JsonTokenType.String:
                    if (reader.TryGetDateTime(out DateTime dateTime))
                        return dateTime;
                    return reader.GetString();
                case JsonTokenType.StartObject:
                    // Create a new dictionary and disable this converter to avoid infinite recursion
                    var nestedOptions = new JsonSerializerOptions(options);
                    foreach (var converter in nestedOptions.Converters)
                    {
                        if (converter is ObjectDictionaryJsonConverter)
                        {
                            nestedOptions.Converters.Remove(converter);
                            break;
                        }
                    }
                    
                    // Read the object manually
                    var nestedDict = new Dictionary<string, object>();
                    var startDepth = reader.CurrentDepth;
                    
                    while (reader.Read() && !(reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == startDepth))
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            var propName = reader.GetString();
                            reader.Read();
                            nestedDict[propName] = ReadValue(ref reader, options);
                        }
                    }
                    
                    return nestedDict;
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        list.Add(ReadValue(ref reader, options));
                    }
                    return list;
                default:
                    throw new JsonException($"Unexpected token: {reader.TokenType}");
            }
        }

        private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            switch (value)
            {
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    break;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    break;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    break;
                case DateTime dateTime:
                    writer.WriteStringValue(dateTime);
                    break;
                case Dictionary<string, object> dict:
                    writer.WriteStartObject();
                    foreach (var kvp in dict)
                    {
                        writer.WritePropertyName(kvp.Key);
                        // Avoid stack overflow by disabling this converter when writing nested dictionaries
                        var nestedOptions = new JsonSerializerOptions(options);
                        if (kvp.Value is Dictionary<string, object>)
                        {
                            foreach (var converter in nestedOptions.Converters)
                            {
                                if (converter is ObjectDictionaryJsonConverter)
                                {
                                    nestedOptions.Converters.Remove(converter);
                                    break;
                                }
                            }
                            // Directly serialize
                            JsonSerializer.Serialize(writer, kvp.Value, nestedOptions);
                        }
                        else
                        {
                            WriteValue(writer, kvp.Value, options);
                        }
                    }
                    writer.WriteEndObject();
                    break;
                case IEnumerable<object> enumerable:
                    writer.WriteStartArray();
                    foreach (var item in enumerable)
                    {
                        WriteValue(writer, item, options);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    var json = JsonSerializer.Serialize(value, value.GetType(), options);
                    {
                        using var doc = JsonDocument.Parse(json);
                        doc.RootElement.WriteTo(writer);
                    }
                    break;
            }
        }
    }
} 