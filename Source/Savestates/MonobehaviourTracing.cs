﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DebugModPlus.Savestates;

internal static class MonobehaviourTracing {
    public static void TraceReferencedMonobehaviours(
        Component origin,
        List<MonoBehaviourSnapshot> saved,
        HashSet<Component> seen,
        int depth = 0,
        int? maxDepth = null,
        int minDepth = 0
    ) {
        if (!origin.gameObject.scene.IsValid()) return;
        if (seen.Contains(origin)) return;

        if (depth >= minDepth) {
            saved.Add(MonoBehaviourSnapshot.Of(origin));
        }

        seen.Add(origin);

        if (maxDepth != null && depth >= maxDepth) {
            return;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var field in origin.GetType().GetFields(flags)) {
            if (
                FindReferenceIgnoreList.Contains(field.FieldType)
                || Array.Exists(FindReferenceIgnoreListBase, x => x.IsAssignableFrom(field.FieldType))
                || field.GetType().Name == "Disc"
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
        typeof(SoundEmitter),
    };

    private static readonly Type[] FindReferenceIgnoreListBase = new[] {
        typeof(IAbstractEventReceiver),
        typeof(ILevelResetPrepare),
        typeof(ILevelResetStart),
    };
}