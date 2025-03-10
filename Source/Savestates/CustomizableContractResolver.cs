﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NineSolsAPI;

namespace DebugModPlus.Savestates;

[PublicAPI]
public class CustomizableContractResolver : DefaultContractResolver {
    public bool ForceReadableWritable = true;

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
            var x = ty.IsGenericType ? ty.GetGenericTypeDefinition() : ty;
            if (FieldAllowlist.TryGetValue(x, out var allowlist)) {
                list.AddRange(allowlist
                    .Select(fieldName => {
                        var field = ty.GetField(fieldName, FieldBindingFlags | BindingFlags.DeclaredOnly);
                        if (field == null) {
                            Log.Error($"Field '{fieldName}' in allowlist of '{ty}' does not exist!");
                        }

                        return field;
                    }).OfType<FieldInfo>());
                continue;
            }


            list.AddRange(ty.GetFields(FieldBindingFlags | BindingFlags.DeclaredOnly));
            list.AddRange(ty
                .GetProperties(PropertyBindingFlags | BindingFlags.DeclaredOnly)
                .Where(x => x.CanWrite && x.CanRead));
        }

        if (FieldDenylist.TryGetValue(objectType, out var denyList)) {
            list.RemoveAll(x => denyList.Contains(x.Name));
        }

        return list;
    }

    private bool IgnorePropertyType(Type? type) =>
        Array.Exists(ExactFieldTypesToIgnore, x => x == type) ||
        Array.Exists(FieldTypesToIgnore, x => x.IsAssignableFrom(type));

    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
        var property = base.CreateProperty(member, memberSerialization);

        property.Ignored = false;
        if (ForceReadableWritable) {
            property.Readable = true;
            property.Writable = true;
        }

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

        if (ContainerTypesToIgnore.Contains(member.DeclaringType)) {
            shouldSerialize = false;
        }

        property.ShouldSerialize = _ => shouldSerialize;

        return property;
    }
}