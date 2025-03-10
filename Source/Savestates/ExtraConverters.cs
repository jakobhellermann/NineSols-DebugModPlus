using System;
using Newtonsoft.Json;

namespace DebugModPlus.Savestates;

public class MappingConverter<T, TU>(Func<T, TU> func) : JsonConverter<T> {
    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer) {
        serializer.Serialize(writer, value == null ? "null" : func(value));
    }

    public override T ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue,
        JsonSerializer serializer) => throw new NotImplementedException();
}

public abstract class NullableJsonConverter<T> : JsonConverter {
    public override sealed void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        WriteJson(writer, (T)value, serializer);
    }

    protected abstract void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer);

    public override sealed object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) {
        if (existingValue != null && existingValue is not T) {
            throw new JsonSerializationException(
                $"Converter cannot read JSON with the specified existing value. {typeof(T)} is required.");
        }

        return ReadJson(reader,
            objectType,
            existingValue == null ? default : (T)existingValue,
            existingValue != null,
            serializer);
    }

    protected abstract T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue,
        JsonSerializer serializer);

    public override sealed bool CanConvert(Type objectType) {
        var underlying = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return typeof(T).IsAssignableFrom(underlying);
    }
}

public abstract class NullableJsonConverter : JsonConverter {
    public override sealed void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        WriteJsonInner(writer, value, serializer);
    }

    protected abstract void WriteJsonInner(JsonWriter writer, object value, JsonSerializer serializer);

    public override sealed object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) {
        if (existingValue != null) {
            throw new JsonSerializationException(
                $"Converter cannot read JSON with the specified existing value.");
        }

        return ReadJsonInner(reader, objectType, existingValue, serializer);
    }

    protected abstract object? ReadJsonInner(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer);

    protected abstract bool CanConvertInner(Type objectType);

    public override sealed bool CanConvert(Type objectType) {
        var underlying = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return CanConvertInner(underlying);
    }
}

public class EnumConverter : NullableJsonConverter {
    public static string EnumToString(object value) {
        var typeName = value.GetType().FullName;
        var name = Enum.GetName(value.GetType(), value);
        return $"{typeName}.{name}";
    }

    protected override void WriteJsonInner(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(EnumToString(value));
    }

    protected override object ReadJsonInner(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer) => throw new NotImplementedException();

    protected override bool CanConvertInner(Type objectType) => objectType.IsEnum;
}