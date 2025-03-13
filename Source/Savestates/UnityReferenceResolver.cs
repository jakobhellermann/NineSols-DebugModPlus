using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DebugModPlus.Savestates;

public class UnityReferenceResolver : IReferenceResolver {
    public string? RelativeTo = null;

    public Type[] InlineReferences = [];

    public Type[] InlineReferencesBase = [];

    public Type[] IgnoredTypes = [];

    public int AlwaysInlineUpToDepth = 0;

    public HashSet<Component> References = [];


    public object ResolveReference(object context, string reference) {
        var component = ObjectUtils.LookupObjectComponentPath(reference);
        if (component == null) {
            throw new JsonSerializationException($"Path {reference} does not exist at snapshot load");
        }

        return component;
    }

    public string GetReference(object context, object value) {
        return "a";
        /*foreach (var ignored in IgnoredTypes) {
            if (ignored.IsInstanceOfType(value)) {
                return "removeme";
            }
        }*/

        if (value is Component component) {
            var id = PostprocessId(component);
            return id;
        }

        return null!;
    }

    private FieldInfo? serializeStack;


    internal Stopwatch stopwatch = new();

    public bool IsReferenced(object context, object value) {
        stopwatch.Start();
        ToastManager.Toast(value);
        if (AlwaysInlineUpToDepth >= 0) {
            serializeStack ??= context.GetType()
                .GetField("_serializeStack", BindingFlags.Instance | BindingFlags.NonPublic);
            if (serializeStack == null) {
                Log.Error("serializeStack not found in context");
            } else {
                var serializeStackVal = (List<object>)serializeStack.GetValue(context);

                if (serializeStackVal.Count <= AlwaysInlineUpToDepth) {
                    stopwatch.Stop();
                    return false;
                }
            }
        }

        if (value is not Component component) {
            stopwatch.Stop();
            return false;
        }

        stopwatch.Stop();
        return true;

        var ty = value.GetType();
        var inline = InlineReferences.Any(x => x == ty) || InlineReferencesBase.Any(x => x.IsAssignableFrom(ty));
        if (inline) {
            return false;
        }

        if (!References.Add(component)) {
        }

        return true;
    }

    public void AddReference(object context, string reference, object value) {
    }

    private string PostprocessId(Component component) {
        var path = ObjectUtils.ObjectPath(component.gameObject);
        var componentName = component.GetType().Name;

        /*if (path.SplitOnce("/LogicRoot/---Boss---") is var (_, after)) {
            path = after;
        }*/
        if (RelativeTo != null) {
            path = path.TrimStartMatches(RelativeTo).ToString();
        }

        return $"{path}@{componentName}";
    }

    public static void Postprocess(JToken token, bool addComponent = true) {
        if (token is not JContainer container) return;

        var removeList = new List<JToken>();
        var addList = new List<(JProperty, JProperty)>();

        foreach (var el in container.Children()) {
            if (el is JProperty { Name: "$ref" } _ref && _ref.Value.ToObject<object>() == null) {
                removeList.Add(el);
            }

            if (el is JProperty { Name: "$id" } p) {
                if (p.Value.ToObject<object>() == null) {
                    removeList.Add(el);
                } else if (addComponent) {
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