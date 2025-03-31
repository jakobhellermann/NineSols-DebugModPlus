using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using NineSolsAPI;
using UnityEngine;
using UnityEngine.Animations;

namespace DebugModPlus.Savestates;

internal static class MonobehaviourTracing {
    public static void TraceReferencedMonobehaviours(
        Component origin,
        List<ComponentSnapshot> saved,
        HashSet<Component> seen,
        Component? onlyDescendantsOf,
        int depth = 0,
        int? maxDepth = null,
        int minDepth = 0
    ) {
        if (!origin.gameObject.scene.IsValid()) return;
        if (seen.Contains(origin)) return;

        if (depth >= minDepth) {
            saved.Add(ComponentSnapshot.Of(origin));
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

            if (field.FieldType.IsArray) {
                var elementType = field.FieldType.GetElementType();
                if (!typeof(Component).IsAssignableFrom(elementType)) continue;

                var values = (Component[])field.GetValue(origin);
                foreach (var arrayValue in values) {
                    if (!arrayValue) continue;

                    if (!onlyDescendantsOf) {
                        TraceReferencedMonobehaviours(arrayValue,
                            saved,
                            seen,
                            onlyDescendantsOf,
                            depth + 1,
                            maxDepth,
                            minDepth);
                    } else if (arrayValue.transform.IsChildOf(onlyDescendantsOf!.transform)) {
                        TraceReferencedMonobehaviours(arrayValue,
                            saved,
                            seen,
                            onlyDescendantsOf,
                            depth + 1,
                            maxDepth,
                            minDepth);
                    } else if (!seen.Contains(arrayValue)) {
                        Log.Info($"Skipping {arrayValue}: not child of {onlyDescendantsOf}");
                    }
                }
            }

            if (!typeof(Component).IsAssignableFrom(field.FieldType)) continue;

            var value = (Component)field.GetValue(origin);
            if (!value) continue;

            if (!onlyDescendantsOf) {
                TraceReferencedMonobehaviours(value, saved, seen, onlyDescendantsOf, depth + 1, maxDepth, minDepth);
            } else if (value.transform.IsChildOf(onlyDescendantsOf!.transform)) {
                TraceReferencedMonobehaviours(value, saved, seen, onlyDescendantsOf, depth + 1, maxDepth, minDepth);
            } else if (!seen.Contains(value)) {
                Log.Info($"Skipping {value}: not child of {onlyDescendantsOf}");
            }
        }
    }

    private static readonly Type[] FindReferenceIgnoreList = [
        typeof(Transform), // maybe

        typeof(EffectDealer),
        typeof(GameLevel),
        typeof(PositionConstraint),
        typeof(PlayerInputCommandQueue),
        typeof(HackDrone),
        typeof(SpriteFlasher),
        typeof(PoolObject),
        typeof(PathArea),
        typeof(DamageScalarSource),
        typeof(PathToAreaFinder),
        typeof(MultiSpriteEffectController),
        typeof(IOnEnableInvokable),
        typeof(OnEnableHierarchyInvoker),
        typeof(EffectReceiver),
        typeof(SoundEmitter),
        typeof(SoundEmitter),
    ];

    private static readonly Type[] FindReferenceIgnoreListBase = [
        typeof(IAbstractEventReceiver),
        typeof(ILevelResetPrepare),
        typeof(ILevelResetStart),
        typeof(Renderer),
        typeof(Collider2D),
    ];
}