using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using Com.LuisPedroFonseca.ProCamera2D;
using DebugModPlus.Savestates;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using IStateMachine = MonsterLove.StateMachine.IStateMachine;
using Object = UnityEngine.Object;

namespace DebugModPlus;

public static class SnapshotSerializer {
    public static void SnapshotRecursive(
        MonoBehaviour origin,
        List<ComponentSnapshot> saved,
        HashSet<Component> seen,
        int? maxDepth = null,
        int minDepth = 0,
        bool onlyDescendants = true
    ) {
        MonobehaviourTracing.TraceReferencedMonobehaviours(origin,
            saved,
            seen,
            onlyDescendants ? origin : null,
            maxDepth: maxDepth,
            minDepth: minDepth);
    }

    public static JToken Snapshot(object obj) => JToken.FromObject(obj, JsonSerializer.Create(Settings));

    public static string SnapshotToString(object? obj) =>
        JsonConvert.SerializeObject(obj, Formatting.Indented, Settings);

    public static void Populate(object target, string json) {
        using JsonReader reader = new JsonTextReader(new StringReader(json));
        JsonSerializer.Create(Settings).Populate(reader, target);
    }

    public static void Populate(object target, JToken json) {
        var serializer = JsonSerializer.Create(Settings);
        using JsonReader reader = new JTokenReader(json);
        serializer.Populate(reader, target);
    }

    private static readonly JsonSerializerSettings Settings = new() {
        ReferenceLoopHandling = ReferenceLoopHandling.Error,
        Error = (_, args) => {
            args.ErrorContext.Handled = true;
            Log.Error(
                $"Serialization error while creating snapshot: {args.CurrentObject?.GetType()}: {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
        },
        ContractResolver = resolver,
        Converters = new List<JsonConverter> {
            new Vector2Converter(),
            new Vector3Converter(),
            new Vector4Converter(),
            new QuatConverter(),
            new ColorConverter(),
            new Color32Converter(),
            new AnimatorConverter(),
            new StringEnumConverter(),
        },
    };

    private static CustomizableContractResolver resolver => new() {
        ContainerTypesToIgnore = [
            typeof(MonoBehaviour),
            typeof(Component),
            typeof(Object),
        ],
        FieldTypesToIgnore = [
            // ignored
            typeof(PoolObject),
            typeof(Camera),
            // typeof(Bounds),
            typeof(GameObject),
            typeof(UnityEventBase),
            typeof(Action),
            typeof(Delegate),
            // typeof(Tweener), TODO only on speedrunpatch?
            typeof(FxPlayer),
            typeof(SoundPlayer),
            typeof(MonsterKnockbackSetting),
            typeof(MonsterFollowBehavior),
            typeof(MappingState.StateEvents),
            typeof(EffectOwner),
            typeof(MoveParam),
            typeof(PositionConstraint),
            typeof(PathArea),
            typeof(AkGameObj),
            typeof(IEffectHitHandler),
            typeof(TextMeshProUGUI),
            typeof(TMP_Text),
            typeof(Image),
            typeof(ICooldownEffectReceiver),
            typeof(VelocityModifierManager),
            typeof(PathToAreaFinder),
            typeof(mixpanel.Value),
            typeof(Sprite),
            typeof(Tilemap),
            typeof(EffectDealer),
            typeof(EffectReceiver),
            typeof(LineRenderer),
            typeof(Color),
            typeof(EffectReceivedPlayerRouter),
            typeof(VelocityModifierParam),
            typeof(ParticleSystem),
            typeof(TestRope.RopeSegment),
            typeof(SoundEmitter),
            typeof(AnimationCurve),
            typeof(MultiSpriteEffectController),
            typeof(AnimationClip),
            typeof(IActiveOverrider),
            typeof(SpriteFlasher),
            typeof(CullingObserver),
            typeof(Rect),
            typeof(IOnEnableInvokable),
            typeof(Timer.DelayTask),
            // todo
            typeof(RCGCullingGroup), // bunch of references
            typeof(PrimeTween.Tween),
            typeof(RenderTexture),
            typeof(Texture2D),
            typeof(Texture3D),
            typeof(DamageDealer),
            // typeof(Rigidbody2D), // maybe
            typeof(Transform), // maybe
            typeof(SpriteRenderer), // maybe
            typeof(LayerMask), // maybe
            typeof(Collider2D), // maybe
            typeof(AbilityWrapper), // bugs out
            typeof(EffectHitData),
            typeof(DelayPositionData),
            typeof(IStateMachine),
            typeof(RuntimeConditionVote),
            typeof(ScriptableObject),
            typeof(StatData),
            typeof(CharacterStat),
            typeof(StatModifier),
            typeof(FxPlayer),
            typeof(MapIndexReference.MapTileData), // maybe
        ],
        ExactFieldTypesToIgnore = [typeof(IResetter), typeof(ILevelDestroy), typeof(ILevelStart), typeof(Component)],
        FieldAllowlist = new Dictionary<Type, string[]> {
            { typeof(Transform), ["localPosition", "localRotation", "localScale"] },
            { typeof(Rigidbody2D), ["position"] }, {
                typeof(ProCamera2D), [
                    "_cameraTargetHorizontalPositionSmoothed",
                    "_cameraTargetVerticalPositionSmoothed",
                    "_previousCameraTargetHorizontalPositionSmoothed",
                    "_previousCameraTargetVerticalPositionSmoothed",
                ]
            },
        },
        FieldDenylist = new Dictionary<Type, string[]> {
            { typeof(StealthGameMonster), ["boxColliderSizes"] },
            { typeof(FlyingMonster), ["boxColliderSizes"] },
            { typeof(MonsterCore), ["AnimationSpeed"] },
            { typeof(ParryCounterDefenseState), ["_context"] },
            { typeof(ConditionTimer.Condition), ["_owner"] },
        },
    };

    internal static void RemoveNullFields(JToken token, params string[] fields) {
        if (token is not JContainer container) return;

        var removeList = new List<JToken>();
        foreach (var el in container.Children()) {
            if (el is JProperty p && fields.Contains(p.Name) && p.Value.ToObject<object>() == null) {
                removeList.Add(el);
            }

            RemoveNullFields(el, fields);
        }

        foreach (var el in removeList) {
            el.Remove();
        }
    }
}