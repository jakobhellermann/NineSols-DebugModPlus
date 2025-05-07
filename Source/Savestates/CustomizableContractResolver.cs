using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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

        if (ContainerTypesToIgnore.Contains(member.DeclaringType)) {
            shouldSerialize = false;
        }

        property.ShouldSerialize = _ => shouldSerialize;

        return property;
    }
}