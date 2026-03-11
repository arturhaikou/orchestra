using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orchestra.Application.Common;

/// <summary>
/// Represents a value that may or may not have been supplied in a JSON payload.
/// HasValue = false  → field was absent from the request (no update intended).
/// HasValue = true   → field was explicitly present (Value may be null to clear the stored value).
/// </summary>
public readonly struct Optional<T>
{
    public T? Value { get; }
    public bool HasValue { get; }

    private Optional(T? value)
    {
        Value = value;
        HasValue = true;
    }

    /// <summary>Represents an absent field.</summary>
    public static Optional<T> None { get; } = default;

    /// <summary>Represents a field that was explicitly provided (value may be null).</summary>
    public static Optional<T> Some(T? value) => new(value);
}

/// <summary>
/// System.Text.Json converter for <see cref="Optional{T}"/>.
/// When the JSON property is present the converter returns Some(value);
/// when the property is absent the default struct value (None) is used automatically.
/// </summary>
public class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    public override Optional<T> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Optional<T>.Some(default);

        var innerConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
        var value = innerConverter.Read(ref reader, typeof(T), options);
        return Optional<T>.Some(value);
    }

    public override void Write(
        Utf8JsonWriter writer,
        Optional<T> value,
        JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        var innerConverter = (JsonConverter<T>)options.GetConverter(typeof(T));
        innerConverter.Write(writer, value.Value!, options);
    }
}

/// <summary>
/// Factory that creates <see cref="OptionalJsonConverter{T}"/> instances for any <see cref="Optional{T}"/>.
/// Register this factory once in the JSON options; all Optional fields are handled automatically.
/// </summary>
public class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var wrappedType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(wrappedType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}
