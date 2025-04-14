﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DebugModPlus.Savestates;

public class Vector4Converter : NullableJsonConverter<Vector4> {
    protected override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WritePropertyName("w");
        writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    protected override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector4>(t.ToString());
        return iv;
    }
}

internal class Vector3Converter : NullableJsonConverter<Vector3> {
    protected override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    protected override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;
    }
}

internal class Vector2Converter : NullableJsonConverter<Vector2> {
    protected override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WriteEndObject();
    }

    protected override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;
    }
}

public class QuatConverter : NullableJsonConverter<Quaternion> {
    protected override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WritePropertyName("w");
        writer.WriteValue(value.w);
        writer.WriteEndObject();
    }

    protected override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Quaternion>(t.ToString());
        return iv;
    }
}

internal class ColorConverter : NullableJsonConverter<Color> {
    protected override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("r");
        writer.WriteValue(value.r);
        writer.WritePropertyName("g");
        writer.WriteValue(value.g);
        writer.WritePropertyName("b");
        writer.WriteValue(value.b);
        writer.WritePropertyName("a");
        writer.WriteValue(value.a);
        writer.WriteEndObject();
    }

    protected override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue,
        JsonSerializer serializer) => throw
        /*var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;*/
        new NotImplementedException();
}

internal class Color32Converter : NullableJsonConverter<Color32> {
    protected override void WriteJson(JsonWriter writer, Color32 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("r");
        writer.WriteValue(value.r);
        writer.WritePropertyName("g");
        writer.WriteValue(value.g);
        writer.WritePropertyName("b");
        writer.WriteValue(value.b);
        writer.WritePropertyName("a");
        writer.WriteValue(value.a);
        writer.WriteEndObject();
    }

    protected override Color32 ReadJson(JsonReader reader, Type objectType, Color32 existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) => throw
        /*var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;*/
        new NotImplementedException();
}

public class AnimatorConverter : NullableJsonConverter<Animator> {
    protected override void WriteJson(JsonWriter writer, Animator? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        var snapshot = AnimatorSnapshot.Snapshot(value);
        serializer.Serialize(writer, snapshot);
    }

    protected override Animator? ReadJson(JsonReader reader, Type objectType, Animator? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        var snapshot = serializer.Deserialize<AnimatorSnapshot>(reader);

        if (snapshot == null) return null;

        if (!hasExistingValue) {
            Log.Error($"Cannot deserialize animator without existing instance at {reader.Path}");
            return null;
        }

        if (existingValue == null) {
            Log.Error($"Cannot deserialize animator with null existing value at {reader.Path}");
            return null;
        }

        snapshot.Restore(existingValue);
        return existingValue;
    }
}