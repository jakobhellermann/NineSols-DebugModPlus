using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace DebugModPlus;

public static class ReflectionUtils {
    public static T? AccessField<T>(this object val, string fieldName) {
        var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

        var field = val.GetType().GetField(fieldName, flags);
        if (field == null) {
            var actualNames = val.GetType().GetFields(flags).Select(x => x.Name).Join(delimiter: ",\n");
            throw new Exception(
                $"Field {fieldName} was not found in type {val.GetType()} or base types \n{actualNames}");
        }

        return (T?)field.GetValue(val);
    }

    public static T? AccessProperty<T>(this object val, string propertyName) =>
        (T?)val.GetType().GetProperty(propertyName)!.GetValue(val);

    public static T AccessBaseField<T>(object val, Type baseType, string fieldName) =>
        (T)baseType
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(val);
}