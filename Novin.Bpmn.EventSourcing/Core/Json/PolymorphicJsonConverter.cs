using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Json
{
    public class PolymorphicJsonConverter<T> : JsonConverter<T> where T : class
    {
        private const string TypeDiscriminatorPropertyName = "$type";

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(T).IsAssignableFrom(typeToConvert);
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject token, got {reader.TokenType}");
            }

            // Parse the JSON document to inspect it
            var readAhead = reader;
            using var jsonDoc = JsonDocument.ParseValue(ref readAhead);
            var rootElement = jsonDoc.RootElement;
            
            if (!rootElement.TryGetProperty(TypeDiscriminatorPropertyName, out var typeProperty))
            {
                // If we don't have a type discriminator, try using the direct type
                return JsonSerializer.Deserialize<T>(ref reader, options);
            }
            
            var typeName = typeProperty.GetString();
            if (string.IsNullOrEmpty(typeName))
            {
                throw new JsonException($"Missing or invalid '{TypeDiscriminatorPropertyName}' property.");
            }

            // Find the concrete type
            var concreteType = Type.GetType(typeName);
            if (concreteType == null)
            {
                // Try prepending core namespaces
                string[] possibleNamespaces = new[]
                {
                    "Novin.Bpmn.EventSourcing.Events.",
                    "Novin.Bpmn.EventSourcing.Core.Models."
                };

                foreach (var ns in possibleNamespaces)
                {
                    concreteType = Type.GetType(ns + typeName);
                    if (concreteType != null) break;
                }

                if (concreteType == null)
                {
                    throw new JsonException($"Cannot find type '{typeName}'.");
                }
            }

            // Make sure the concrete type is assignable to T
            if (!typeof(T).IsAssignableFrom(concreteType))
            {
                throw new JsonException($"Type '{concreteType.FullName}' is not assignable to '{typeof(T).FullName}'.");
            }

            // Deserialize the concrete type
            return (T)JsonSerializer.Deserialize(ref reader, concreteType, options);
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var actualType = value.GetType();
            var writerOptions = new JsonSerializerOptions(options);
            
            // Disable this converter to avoid circular reference
            foreach (var converter in writerOptions.Converters)
            {
                if (converter is PolymorphicJsonConverter<T>)
                {
                    writerOptions.Converters.Remove(converter);
                    break;
                }
            }

            var memoryStream = new System.IO.MemoryStream();
            var tempWriter = new Utf8JsonWriter(memoryStream);
            JsonSerializer.Serialize(tempWriter, value, actualType, writerOptions);
            tempWriter.Flush();

            memoryStream.Position = 0;
            using var doc = JsonDocument.Parse(memoryStream);
            writer.WriteStartObject();
            
            // Add the $type property
            writer.WriteString(TypeDiscriminatorPropertyName, actualType.FullName);
            
            // Copy all existing properties
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }
            
            writer.WriteEndObject();
        }
    }
} 