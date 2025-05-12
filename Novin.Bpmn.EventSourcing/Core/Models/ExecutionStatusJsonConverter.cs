using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novin.Bpmn.EventSourcing.Core.Models
{
    public class ExecutionStatusJsonConverter : JsonConverter<ExecutionStatus>
    {
        public override ExecutionStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string statusName = reader.GetString();
                return Enum.TryParse<ExecutionStatus>(statusName, true, out var status) 
                    ? status 
                    : ExecutionStatus.Active;
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                int statusValue = reader.GetInt32();
                return Enum.IsDefined(typeof(ExecutionStatus), statusValue) 
                    ? (ExecutionStatus)statusValue 
                    : ExecutionStatus.Active;
            }
            
            throw new JsonException($"Unexpected token {reader.TokenType} when parsing ExecutionStatus");
        }

        public override void Write(Utf8JsonWriter writer, ExecutionStatus value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
} 