using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using IStateMachine = MonsterLove.StateMachine.IStateMachine;
using Object = UnityEngine.Object;

namespace DebugModPlus;

public static class SnapshotSerializer {
    public static JToken Snapshot(object obj) => JToken.FromObject(obj, JsonSerializer.Create(Settings));

    public static string SnapshotToString(object? obj) =>
        JsonConvert.SerializeObject(obj, Formatting.Indented, Settings);

    public static void Populate(object target, string json) {
        using JsonReader reader = new JsonTextReader(new StringReader(json));
        JsonSerializer.Create(Settings).Populate(reader, target);
    }

    public static void Populate(object target, JToken json) {
        var serializer = JsonSerializer.Create(Settings);
        using JsonReader reader = new JTokenReader(json);
        serializer.Populate(reader, target);
    }

    private static readonly JsonSerializerSettings Settings = new() {
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        Error = (_, args) => {
            args.ErrorContext.Handled = true;
            Log.Error(
                $"Serialization error while creating snapshot: {args.CurrentObject?.GetType()}: {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
        },
        ContractResolver = new SnapshotStateResolver(),
        Converters = new List<JsonConverter> {
            new Vector2Converter(),
            new Vector3Converter(),
            new Vector4Converter(),
            new QuatConverter(),
            new AnimatorConverter(),
        },
    };

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);
}

file class SnapshotStateResolver : DefaultContractResolver {
    // checks exact
    private readonly Type[] containerTypesToIgnore = [
        typeof(MonoBehaviour),
        typeof(Component),
        typeof(Object),
    ];

    // checks IsAssignableFrom
    private readonly Type[] fieldTypesToIgnore = [
        // ignored
        typeof(PoolObject),
        typeof(MonoBehaviour),
        typeof(GameObject),
        typeof(UnityEventBase),
        typeof(Action),
        typeof(Delegate),
        typeof(FxPlayer),
        typeof(MappingState.StateEvents),
        typeof(IEffectOwner),
        typeof(PositionConstraint),
        typeof(PathArea),
        typeof(IEffectHitHandler),
        typeof(ICooldownEffectReceiver),
        typeof(PathToAreaFinder),
        typeof(mixpanel.Value),
        typeof(Sprite),
        typeof(Tilemap),
        typeof(LineRenderer),
        typeof(Color),
        typeof(VelocityModifierParam),
        typeof(ParticleSystem),
        typeof(TestRope.RopeSegment),
        // todo
        typeof(Rigidbody2D), // maybe
        typeof(Transform), // maybe
        typeof(SpriteRenderer), // maybe
        typeof(LayerMask), // maybe
        typeof(Collider2D), // maybe
        typeof(AbilityWrapper), // bugs out
        typeof(EffectHitData),
        typeof(IStateMachine),
        typeof(RuntimeConditionVote),
        typeof(ScriptableObject),
        typeof(StatData),
        typeof(CharacterStat),
        typeof(StatModifier),
        typeof(MapIndexReference.MapTileData), // maybe
    ];

    private Type? declaredOnly = null;

    protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
        var ty = declaredOnly is { } d && d.IsAssignableFrom(objectType) ? d : objectType;
        var extraFlags = declaredOnly != null ? BindingFlags.DeclaredOnly : BindingFlags.Default;

        var fields = ty
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | extraFlags)
            .Cast<MemberInfo>();
        var properties = ty
            .GetProperties(BindingFlags.Instance | BindingFlags.Public /* | BindingsFlags.NonPublic */ | extraFlags)
            .Where(x => x.CanWrite && x.CanRead)
            .Cast<MemberInfo>();

        return fields.Concat(properties).ToList();
    }

    private bool IgnorePropertyType(Type? type) => Array.Exists(fieldTypesToIgnore, x => x.IsAssignableFrom(type));

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);
        property.Readable = true;
        property.Writable = true;
        property.Ignored = false;

        // var shouldSerialize = property.Writable;
        var shouldSerialize = true;

        var type = property.PropertyType;
        if (type == null) return property;


        shouldSerialize &= !IgnorePropertyType(type);

        if (type.IsArray) {
            shouldSerialize &= !IgnorePropertyType(type.GetElementType());
        }

        if (type.IsGenericType) {
            if (type.GetGenericTypeDefinition() == typeof(List<>)) {
                shouldSerialize &= !IgnorePropertyType(type.GetGenericArguments()[0]);

                property.ObjectCreationHandling = ObjectCreationHandling.Replace;
            }

            if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                var generics = type.GetGenericArguments();
                shouldSerialize &= generics[0].IsPrimitive || generics[0] == typeof(string);
                shouldSerialize &= !IgnorePropertyType(type.GetGenericArguments()[1]);
            }

            if (type.GetGenericTypeDefinition() == typeof(HashSet<>)) {
                shouldSerialize &= !IgnorePropertyType(type.GetGenericArguments()[0]);
            }
        }

        if (containerTypesToIgnore.Contains(member.DeclaringType)) {
            shouldSerialize = false;
        }

        // ToastManager.Toast(shouldSerialize);
        property.ShouldSerialize = _ => shouldSerialize;

        return property;
    }
}

file class AnimatorConverter : JsonConverter<Animator> {
    public override void WriteJson(JsonWriter writer, Animator? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        var snapshot = AnimatorSnapshot.Snapshot(value);
        serializer.Serialize(writer, snapshot);
    }

    public override Animator? ReadJson(JsonReader reader, Type objectType, Animator? existingValue,
        bool hasExistingValue, JsonSerializer serializer) {
        if (!hasExistingValue) {
            Log.Error("Cannot deserialize animator without existing instance");
            return null;
        }

        if (existingValue == null) {
            Log.Error("Cannot deserialize animator with null existing value");
            return null;
        }

        var snapshot = serializer.Deserialize<AnimatorSnapshot>(reader)!;
        snapshot.Restore(existingValue);

        return existingValue;
    }
}

file class Vector4Converter : JsonConverter<Vector4> {
    public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer) {
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

    public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector4>(t.ToString());
        return iv;
    }
}

internal class Vector3Converter : JsonConverter<Vector3> {
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WritePropertyName("z");
        writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;
    }
}

internal class Vector2Converter : JsonConverter<Vector2> {
    public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer) {
        writer.WriteStartObject();
        writer.WritePropertyName("x");
        writer.WriteValue(value.x);
        writer.WritePropertyName("y");
        writer.WriteValue(value.y);
        writer.WriteEndObject();
    }

    public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Vector3>(t.ToString());
        return iv;
    }
}

file class QuatConverter : JsonConverter<Quaternion> {
    public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer) {
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

    public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        var t = serializer.Deserialize(reader)!;
        var iv = JsonConvert.DeserializeObject<Quaternion>(t.ToString());
        return iv;
    }
}