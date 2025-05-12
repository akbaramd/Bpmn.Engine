using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    public class ProcessInstanceStatusJsonConverter : JsonConverter<ProcessInstanceStatus>
    {
        public override ProcessInstanceStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string statusName = reader.GetString();
                return ProcessInstanceStatus.FromString(statusName);
            }
            
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing ProcessInstanceStatus");
        }

        public override void Write(Utf8JsonWriter writer, ProcessInstanceStatus value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
} 