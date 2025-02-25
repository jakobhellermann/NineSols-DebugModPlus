using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DebugModPlus.Savestates;

public static class MonobehaviourTracing {
    public static void TraceReferencedMonobehaviours(
        MonoBehaviour origin,
        List<MonoBehaviourSnapshot> saved,
        HashSet<MonoBehaviour> seen,
        int depth = 0,
        int maxDepth = 0,
        int minDepth = 0
    ) {
        if (seen.Contains(origin)) return;

        if (depth >= minDepth) {
            saved.Add(MonoBehaviourSnapshot.Of(origin));
        }

        seen.Add(origin);

        if (depth >= maxDepth) {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var field in origin.GetType().GetFields(flags)) {
            if (
                FindReferenceIgnoreList.Contains(field.FieldType)
                || Array.Exists(FindReferenceIgnoreListBase, x => x.IsAssignableFrom(field.FieldType))
            ) {
                continue;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(field.FieldType)) continue;

            var value = (MonoBehaviour)field.GetValue(origin);
            if (!value) continue;

            TraceReferencedMonobehaviours(value, saved, seen, depth + 1, maxDepth, minDepth);
        }
    }

    private static readonly Type[] FindReferenceIgnoreList = new[] {
        typeof(EffectDealer),
        typeof(PlayerInputCommandQueue),
        typeof(HackDrone),
        typeof(SpriteFlasher),
        typeof(PoolObject),
        typeof(PathArea),
        typeof(DamageScalarSource),
        typeof(PathToAreaFinder),
        typeof(IOnEnableInvokable),
        typeof(OnEnableHierarchyInvoker),
        typeof(EffectReceiver),
        typeof(SoundEmitter),
    };

    private static readonly Type[] FindReferenceIgnoreListBase = new[] {
        typeof(IAbstractEventReceiver),
    };
}