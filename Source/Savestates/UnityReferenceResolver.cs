using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DebugModPlus.Savestates;

public class UnityReferenceResolver : IReferenceResolver {
    public Type[] InlineReferences = [];

    public Type[] InlineReferencesBase = [];
    private const int AlwaysInlineUpToDepth = 2;


    public object ResolveReference(object context, string reference) => throw new NotImplementedException();

    public string GetReference(object context, object value) {
        if (value is Component component) {
            return ObjectUtils.ObjectComponentPath(component);
        }

        return null!;
    }


    public bool IsReferenced(object context, object value) {
        var field = context.GetType().GetField("_serializeStack", BindingFlags.Instance | BindingFlags.NonPublic);
        var root = false;
        if (field == null) {
            Log.Error("serializeStack not found in context");
        } else {
            var serializeStack = (List<object>)field.GetValue(context);
            root = serializeStack.Count <= AlwaysInlineUpToDepth;
        }

        if (root) {
            return false;
        }

        if (value is not Component) return false;

        var ty = value.GetType();
        var inline = InlineReferences.Any(x => x == ty) || InlineReferencesBase.Any(x => x.IsAssignableFrom(ty));
        return !inline;
    }

    public void AddReference(object context, string reference, object value) {
        throw new NotImplementedException();
    }
}