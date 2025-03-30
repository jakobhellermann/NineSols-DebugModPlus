using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

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


            list.AddRange(ty.GetFields(FieldBindingFlags | BindingFlags.DeclaredOnly));
            list.AddRange(ty
                .GetProperties(PropertyBindingFlags | BindingFlags.DeclaredOnly)
                .Where(prop => prop.CanWrite && prop.CanRead));
        }

        if (FieldDenylist.TryGetValue(objectType, out var denyList)) {
            list.RemoveAll(field => denyList.Contains(field.Name));
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