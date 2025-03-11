#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using I2.Loc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NineSolsAPI;

namespace DebugModPlus.Modules;

public static class FlagLoggerModule {
    private const bool LogFlagsOnAwake = false;

    public static void Awake() {
        if (LogFlagsOnAwake) LogFlags();
    }

    [BindableMethod]
    public static void LogFlags() {
        try {
            var json = JsonConvert.SerializeObject(GameConfig.Instance.allGameFlags.flagDict,
                Formatting.Indented,
                new JsonSerializerSettings {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    ContractResolver =
                        new IgnoreTypeContractResolver(
                            ["AsyncOperationHandle"],
                            [
                                "onChangeActionDict", "assetReference", "CurrentValue",
                            ]
                        ),
                    Converters = [
                        new GameFlagConverter(),
                        new FlagFieldConverter<FlagFieldBool, bool>(),
                        new FlagFieldConverter<FlagFieldInt, int>(),
                        new FlagFieldConverter<FlagFieldFloat, float>(),
                        new FlagFieldConverter<FlagFieldLong, long>(),
                        new FlagFieldConverter<FlagFieldString, string>(),
                        new FuncConverterImperative<ScriptableDataBool>((x, writer, serializer) => {
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            serializer.Serialize(writer, x.name);
                            writer.WritePropertyName("value");
                            serializer.Serialize(writer, x.CurrentValue);
                            writer.WriteEndObject();
                        }),
                        new FuncConverterImperative<ScriptableDataFloat>((x, writer, serializer) => {
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            serializer.Serialize(writer, x.name);
                            writer.WritePropertyName("value");
                            serializer.Serialize(writer, x.CurrentValue);
                            writer.WriteEndObject();
                        }),
                        new FuncConverterImperative<GameFlagInt>((x, writer, serializer) => {
                            writer.WriteStartObject();
                            writer.WritePropertyName("name");
                            serializer.Serialize(writer, x.name);
                            writer.WritePropertyName("value");
                            serializer.Serialize(writer, x.CurrentValue);
                            writer.WriteEndObject();
                        }),
                        new FuncConverter<LocalizedString, string>(x => x.ToString()),
                        new FuncConverter<GuidReference, Guid>(x => x.Guid),
                        new FuncConverter<StatModType, string>(x => x.ToString()),
                        new FuncConverter<StatModDurationType, string>(x => x.ToString()),
                        new FuncConverter<PoolObject, string>(x => x.name),

                        new FuncConverterImperative<CharacterStat>((stat, writer, serializer) => {
                            writer.WriteStartObject();

                            writer.WritePropertyName("BaseValue");
                            serializer.Serialize(writer, stat.BaseValue);

                            writer.WritePropertyName("StatModifiers");
                            serializer.Serialize(writer, stat.StatModifiers);

                            writer.WritePropertyName("COMPUTED_FinalValue");
                            serializer.Serialize(writer, stat.Value);

                            writer.WriteEndObject();
                        }),
                    ],
                });

            File.WriteAllText("/tmp/allFlags.json", json);
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }
}

file class IgnoreTypeContractResolver(string[] typesToIgnore, string[] fieldsToIgnore) : DefaultContractResolver {
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);

        if (Array.Exists(typesToIgnore, t => property.PropertyType?.Name == t))
            property.ShouldSerialize = _ => false;
        return property;
    }

    /*protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var properties = base.CreateProperties(type, memberSerialization);


        foreach (var property in properties)
            if (fieldsToIgnore.Contains(property.PropertyName))
                property.ShouldSerialize = _ => false;

        return properties;
    }*/

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Instance);

        var jsonProperties = new List<JsonProperty>();
        foreach (var field in fields) {
            if (fieldsToIgnore.Contains(field.Name)) continue;

            var jsonProperty = base.CreateProperty(field, memberSerialization);
            jsonProperty.Readable = true;
            jsonProperty.Writable = true;
            jsonProperties.Add(jsonProperty);
        }

        return jsonProperties;
    }
}

file class FlagFieldConverter<TContainer, T> : JsonConverter<TContainer> where TContainer : FlagField<T> {
    public override void WriteJson(JsonWriter writer, TContainer? value, JsonSerializer serializer) {
        serializer.Serialize(writer, value != null ? value.CurrentValue : null);
    }

    public override TContainer? ReadJson(JsonReader reader, Type objectType, TContainer? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) =>
        throw new NotImplementedException();
}

public class FuncConverter<T, TU>(Func<T, TU> func) : JsonConverter<T> {
    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer) {
        serializer.Serialize(writer, value == null ? "null" : func(value));
    }

    public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue,
        JsonSerializer serializer) => throw new NotImplementedException();
}

file class FuncConverterImperative<T>(Action<T, JsonWriter, JsonSerializer> func) : JsonConverter<T> {
    public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        func(value, writer, serializer);
    }

    public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue,
        JsonSerializer serializer) => throw new NotImplementedException();
}

/*file class RootDifferentConverter(JsonConverter root, JsonConverter nested) : JsonConverter<GameFlagBase> {
    public override void WriteJson(JsonWriter writer, GameFlagBase? value, JsonSerializer serializer) {
        var isRootNode = writer.Path.Split('.').Length <= 1;

        if (isRootNode)
            root.WriteJson(writer, value, serializer);
        else
            nested.WriteJson(writer, value, serializer);
    }

    public override GameFlagBase? ReadJson(JsonReader reader, Type objectType, GameFlagBase? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) => throw new NotImplementedException();
}*/

file class GameFlagConverter : JsonConverter<GameFlagBase> {
    public override void WriteJson(JsonWriter writer, GameFlagBase? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }


        var isRootNode = writer.Path.Split('.').Length <= 1;

        if (isRootNode) {
            foreach (var converter in serializer.Converters)
                if (converter != this && converter.CanConvert(value.GetType())) {
                    converter.WriteJson(writer, value, serializer);
                    return;
                }

            writer.WriteStartObject();

            writer.WritePropertyName("name");
            writer.WriteValue(value.name);

            serializer.Serialize(writer, value);
            foreach (var field in value.GetType()
                         .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                if (field.Name is "propertyCache" or "fieldCaches" or "gameStateType" or "variableEntries"
                    or "_variableEntriesIncludeZDoorMaps"
                    or "MistMapDataEntries" or "weaponPrefab")
                    continue;

                writer.WritePropertyName(field.Name);
                var propValue = field.GetValue(value);
                serializer.Serialize(writer, propValue);
            }

            writer.WriteEndObject();
        } else
            writer.WriteValue($"flagref:{value.FinalSaveID}");
    }

    public override GameFlagBase? ReadJson(JsonReader reader, Type objectType, GameFlagBase? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) => throw new NotImplementedException();
}