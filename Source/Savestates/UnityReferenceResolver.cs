using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DebugModPlus.Utils;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DebugModPlus.Savestates;

public class UnityReferenceResolver : IReferenceResolver {
    public string? RelativeTo = null;

    public Type[] InlineReferences = [];

    public Type[] InlineReferencesBase = [];
    private const int AlwaysInlineUpToDepth = 2;


    public object ResolveReference(object context, string reference) => throw new NotImplementedException();

    public string GetReference(object context, object value) {
        if (value is Component component) {
            return PostprocessId(component);
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

    private string PostprocessId(Component component) {
        var path = ObjectUtils.ObjectPath(component.gameObject);
        var componentName = component.name;

        /*if (path.SplitOnce("/LogicRoot/---Boss---") is var (_, after)) {
            path = after;
        }*/
        if (RelativeTo != null) {
            path = path.TrimStartMatches(RelativeTo).ToString();
        }

        return $"{path}@{componentName}";
    }

    public static void Postprocess(JToken token) {
        if (token is not JContainer container) return;

        var removeList = new List<JToken>();
        var addList = new List<(JProperty, JProperty)>();

        foreach (var el in container.Children()) {
            if (el is JProperty { Name: "$id" } p) {
                if (p.Value.ToObject<object>() == null) {
                    removeList.Add(el);
                } else {
                    var id = p.Value.Value<string>()!;
                    var (_, component) = id.SplitOnce('@')!.Value;
                    addList.Add((p, new JProperty("$component", component)));
                }
            }

            Postprocess(el);
        }

        foreach (var el in removeList) {
            el.Remove();
        }

        foreach (var (after, add) in addList) {
            after.AddAfterSelf(add);
        }
    }
}