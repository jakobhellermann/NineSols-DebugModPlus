using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NineSolsAPI.Utils;
using UnityEngine;
using UnityEngine.Bindings;

namespace DebugModPlus.Savestates;

[PublicAPI]
public class CustomizableContractResolver : DefaultContractResolver {
    public BindingFlags FieldBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public BindingFlags PropertyBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public Dictionary<Type, string[]> FieldAllowlist = new();
    public Dictionary<Type, string[]> FieldDenylist = new();

    // checks exact
    public Type[] ContainerTypesToIgnore = [];

    // checks IsAssignableFrom
    public Type[] FieldTypesToIgnore = [];
    public Type[] ExactFieldTypesToIgnore = [];

    protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
        var list = new List<MemberInfo>();

        for (var ty = objectType; ty != typeof(object) && ty != null; ty = ty.BaseType) {
            var typeStem = ty.IsGenericType ? ty.GetGenericTypeDefinition() : ty;
            if (FieldAllowlist.TryGetValue(typeStem, out var allowlist)) {
                list.AddRange(allowlist
                    .Select(fieldName => {
                        var field = (MemberInfo?)ty.GetField(fieldName, FieldBindingFlags | BindingFlags.DeclaredOnly)
                                    ?? ty.GetProperty(fieldName, PropertyBindingFlags | BindingFlags.DeclaredOnly);
                        if (field == null) {
                            Log.Error($"Field '{fieldName}' in allowlist of '{ty}' does not exist!");
                        }

                        return field;
                    }).Cast<MemberInfo>());
                continue;
            }


            list.AddRange(ty.GetFields(FieldBindingFlags | BindingFlags.DeclaredOnly)
                .Where(field => field.GetCustomAttribute<CompilerGeneratedAttribute>() == null));
            list.AddRange(ty
                .GetProperties(PropertyBindingFlags | BindingFlags.DeclaredOnly)
                .Where(prop => prop.CanWrite && prop.CanRead
                                             && (prop.GetGetMethod() is { IsVirtual: true } ||
                                                 prop.GetCustomAttribute<NativePropertyAttribute>() != null)));

            // TODO: just removed added from this type
            if (FieldDenylist.TryGetValue(ty, out var denyList)) {
                list.RemoveAll(field => denyList.Contains(field.Name));
            }
        }

        return list;
    }

    protected override JsonContract CreateContract(Type objectType) {
        if (objectType == typeof(Transform)) {
            // Transform is IEnumerable which lets newtonsoft treat it as Array
            return base.CreateObjectContract(objectType);
        }

        return base.CreateContract(objectType);
    }

    private bool IgnorePropertyType(Type? type) =>
        Array.Exists(ExactFieldTypesToIgnore, x => x == type) ||
        Array.Exists(FieldTypesToIgnore, x => x.IsAssignableFrom(type));

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        // default MemberSerialization ignores private fields, unless DefaultMembersSearchFlags.NonPublic ist set,
        // but that field is deprecated in favor of GetSerializableMembers.
        var property = base.CreateProperty(member, MemberSerialization.Fields);

        property.Ignored = false;

        var shouldSerialize = true;

        var type = property.PropertyType;
        if (type == null) return property;

        if (member.GetCustomAttribute<AutoAttribute>() != null ||
            member.GetCustomAttribute<AutoChildrenAttribute>() != null) {
            shouldSerialize = false;
        }

        shouldSerialize &= !IgnorePropertyType(type);

        if (type.IsArray) {
            shouldSerialize &= !IgnorePropertyType(type.GetElementType());
        }

        var itemType = type;

        if (type.IsArray) {
            itemType = type.GetElementType()!;
            shouldSerialize &= !IgnorePropertyType(itemType);
        } else if (type.IsGenericType) {
            if (type.GetGenericTypeDefinition() == typeof(List<>)) {
                itemType = type.GetGenericArguments()[0];
                shouldSerialize &= !IgnorePropertyType(itemType);

                property.ObjectCreationHandling = ObjectCreationHandling.Replace;
            }

            if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>)) {
                var generics = type.GetGenericArguments();
                // TODO Dict<EffectDealer, bool>
                shouldSerialize &= generics[0].IsPrimitive || generics[0] == typeof(string);
                itemType = type.GetGenericArguments()[1];
                shouldSerialize &= !IgnorePropertyType(itemType);
            }

            if (type.GetGenericTypeDefinition() == typeof(HashSet<>)) {
                itemType = type.GetGenericArguments()[0];
                shouldSerialize &= !IgnorePropertyType(itemType);
            }
        }

        if (RefType(itemType)) {
            property.Converter = new RefConverter();
        }

        if (ContainerTypesToIgnore.Contains(member.DeclaringType)) {
            shouldSerialize = false;
        }

        property.ShouldSerialize = _ => shouldSerialize;

        return property;
    }

    private bool RefType(Type type) => typeof(Component).IsAssignableFrom(type) && type != typeof(Animator);
}

internal class RefConverter : JsonConverter {
    private static void WriteReference(JsonWriter writer, object? component) {
        if (component == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("$ref");
        writer.WriteValue(GetRef(component));
        writer.WriteEndObject();
    }

    private static Component? ReadReference(JsonReader reader) {
        if (reader.TokenType == JsonToken.Null) return null;

        if (reader.TokenType != JsonToken.StartObject)
            throw new JsonSerializationException($"Expected StartObject token, got {reader.TokenType}");

        string? refPath = null;
        while (reader.Read()) {
            if (reader.TokenType == JsonToken.PropertyName && reader.Value?.ToString() == "$ref") {
                reader.Read();
                refPath = reader.Value?.ToString();
                break;
            }
        }

        if (refPath == null) {
            throw new JsonSerializationException($"Expected $ref property at {reader.Path}");
        }

        while (reader.Read() && reader.TokenType != JsonToken.EndObject) {
        }

        return ObjectUtils.LookupObjectComponentPath(refPath);
    }

    private static IEnumerable<Component?> ReadReferenceArray(JsonReader reader) {
        if (reader.TokenType != JsonToken.StartArray)
            throw new JsonSerializationException($"Expected StartArray token, got {reader.TokenType}");

        reader.Read();

        while (reader.TokenType != JsonToken.EndArray) {
            yield return ReadReference(reader);

            reader.Read();
        }
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        if (value is Component component) {
            if (!component) {
                throw new NotImplementedException("!component");
            }

            WriteReference(writer, value);
        } else if (value is IEnumerable list) {
            writer.WriteStartArray();
            foreach (var item in list) {
                WriteReference(writer, item);
            }

            writer.WriteEndArray();
        } else {
            throw new NotImplementedException($"RefConverter got {value.GetType()}");
        }
    }


    public override object? ReadJson(JsonReader reader, Type type, object? existingValue,
        JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) {
            return null;
        }

        if (type.IsArray) {
            var arr = ReadReferenceArray(reader).ToArray();
            var newArray = Array.CreateInstance(type.GetElementType()!, arr.Length);
            for (var i = 0; i < arr.Length; i++) {
                newArray.SetValue(arr[i], i);
            }

            return newArray;
        } else if (typeof(IList).IsAssignableFrom(type)) {
            if (existingValue is not IList list) throw new JsonSerializationException("List requires existing value");

            list.Clear();
            foreach (var item in ReadReferenceArray(reader)) {
                list.Add(item);
            }

            return list;
        } else if (type.IsGenericType) {
            if (existingValue is null)
                throw new JsonSerializationException("reference collection requires existing value");

            if (type.GetGenericTypeDefinition() == typeof(HashSet<>)) {
                existingValue.InvokeMethod("Clear");
                foreach (var item in ReadReferenceArray(reader)) {
                    existingValue.InvokeMethod<bool>("Add", item);
                }

                return existingValue;
            }

            throw new NotImplementedException($"RefConverter called for {type}");
        } else if (typeof(Component).IsAssignableFrom(type)) {
            return ReadReference(reader);
        } else {
            throw new NotImplementedException($"RefConverter called for {type}");
        }
    }

    // we only assign the in the contract resolver
    public override bool CanConvert(Type objectType) => true;

    private static string GetRef(object obj) {
        if (obj is Component component) {
            return ObjectUtils.ObjectComponentPath(component);
        }

        throw new NotImplementedException("GetRef not for component");
    }
}