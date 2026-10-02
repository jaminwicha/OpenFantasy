using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Each registration names one concrete value shape; no authority or polymorphic type activation exists.</summary>
internal sealed class ObservationValueConverter<TView, TData>(Func<TView, TData> capture, Func<TData, TView> restore)
    : JsonConverter<TView> where TView : class where TData : class
{
    public override TView Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        restore(JsonSerializer.Deserialize<TData>(ref reader, options) ?? throw new JsonException("Missing observation value."));

    public override void Write(Utf8JsonWriter writer, TView value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, capture(value), options);
}

internal sealed class ObservationCommandHashConverter : JsonConverter<CommandContentHash>
{
    public override CommandContentHash Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        CommandContentHash.Parse(reader.GetString() ?? throw new JsonException("Missing command hash."));
    public override void Write(Utf8JsonWriter writer, CommandContentHash value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class ObservationCollectionConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(ReadOnlyCollection<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(FrozenCollectionConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class FrozenCollectionConverter<T> : JsonConverter<ReadOnlyCollection<T>>
    {
        public override ReadOnlyCollection<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Array.AsReadOnly(JsonSerializer.Deserialize<T[]>(ref reader, options) ?? throw new JsonException("Missing collection."));
        public override void Write(Utf8JsonWriter writer, ReadOnlyCollection<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}
