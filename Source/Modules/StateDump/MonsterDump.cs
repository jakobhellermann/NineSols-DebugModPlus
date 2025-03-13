using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DebugModPlus.Savestates;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// ReSharper disable NotAccessedField.Local

namespace DebugModPlus.Modules.StateDump;

internal record MonsterBaseData {
    public required MonsterBase.States InitState;
    public required Dictionary<string, BossGeneralState> BossGeneralStates;
    public required Dictionary<string, MappingState> OtherStates;
    public required MonsterHurtInterrupt HurtInterrupt;
    public required TeleportBinding? TeleportBinding;
    public required AttackSensor[] AttackSensors;

    // public required MonsterStateSequenceWeight[]? PhaseSequences;
}

public static class MonsterDump {
    private const string DumpDir = "C:/Users/Jakob/Documents/dev/nine-sols/StateDump";

    private static HashSet<MonsterStat> seenNames = [];

    public static void DumpAllMonsters() {
        var monsters = new List<(string, MonsterBase)>();

        string? singleMonsterNameInScene = null;
        foreach (var monster in MonsterManager.Instance.monsterDict.Values) {
            var monsterName = monster.monsterStat.monsterName.ToString();
            if (monsterName == "") continue;

            if (singleMonsterNameInScene != null && singleMonsterNameInScene != monsterName) {
                singleMonsterNameInScene = null;
                break;
            }

            singleMonsterNameInScene = monsterName;
        }


        ToastManager.Toast(seenNames.Count);
        foreach (var monster in MonsterManager.Instance.monsterDict.Values) {
            var monsterName = monster.monsterStat.monsterName.ToString();
            var fullName = monster.name.TrimStartMatches("StealthGameMonster_").TrimStartMatches("TrapMonster_")
                .ToString();
            fullName = new Regex(@"( ?\(\d+\)|\(Clone\))$").Replace(fullName, "");

            string? name;
            if (monsterName != "") {
                name = monsterName;
            } else {
                name = fullName;
                if (singleMonsterNameInScene != null) name = $"{singleMonsterNameInScene}-{fullName}";
            }

            name = name.Replace(":", "");

            if (seenNames.Add(monster.monsterStat)) {
                monsters.Add((name, monster));
            }
        }

        foreach (var (fileName, monster) in monsters) {
            DumpMonster(fileName, monster);
        }
    }

    private static void DumpMonster(string fileName, MonsterBase monster) {
        ToastManager.Toast($"Dumping {fileName}");
        var level = monster.monsterStat.monsterLevel;
        var dumpDir = Path.Combine(DumpDir, "Attacks", level.ToString(), fileName);
        Directory.CreateDirectory(dumpDir);

        // var statesDir = Path.Combine(dumpDir, "states");

        /*foreach (var entry in monster.fsm._stateMapping.getAllStates) {
            if (entry.stateBehavior is not BossGeneralState state) continue;

            File.WriteAllText(Path.Combine(statesDir, state.name) + ".json", TestSerialize(state));
        }*/

        /*var allBGS = monster.fsm._stateMapping.getAllStates
            .Select(x => x.stateBehavior)
            .OfType<BossGeneralState>();
        File.WriteAllText(Path.Combine(dumpDir, "allAttacks") + ".json", TestSerialize(allBGS));


        File.WriteAllText(Path.Combine(dumpDir, "attackSequence") + ".json", TestSerialize(sequences));*/
        var sequences =
            monster.monsterCore.attackSequenceMoodule.GetFieldValue<MonsterStateSequenceWeight[]>(
                "SequenceForDifferentPhase");

        var data = new MonsterBaseData {
            // PhaseSequences = sequences.All(x => x.setting.stateWeightList.Count == 0) ? null : sequences,
            BossGeneralStates = monster.fsm._stateMapping.getAllStates
                .Where(x => x.stateBehavior is BossGeneralState)
                .ToDictionary(x => EnumConverter.EnumToString(x.state), x => (BossGeneralState)x.stateBehavior),
            OtherStates = monster.fsm._stateMapping.getAllStates
                .Where(x => x.stateBehavior is not BossGeneralState)
                .ToDictionary(x => EnumConverter.EnumToString(x.state), x => x.stateBehavior),
            AttackSensors = monster.attackSensors.ToArray(),
            InitState = monster.initState,
            HurtInterrupt = monster.HurtInterrupt,
            TeleportBinding = monster.monsterCore.teleportBinding,
        };

        referenceResolver.RelativeTo = ObjectUtils.ObjectPath(monster.gameObject);
        var snapshot = JToken.FromObject(data, testSerializer);
        UnityReferenceResolver.Postprocess(snapshot);
        var text = snapshot.ToString(Formatting.Indented);

        File.WriteAllText(Path.Combine(dumpDir, "data.json"), text);
    }


    private static UnityReferenceResolver referenceResolver = new() {
        InlineReferences = [
            typeof(LinkNextMoveStateWeight),
            typeof(LinkMoveGroupingNode),
            typeof(MonsterStateQueue),
            typeof(MonsterStateSequenceWeight),
            typeof(MonsterStateGroupSequence),
            typeof(MonsterStateGroup),
            // typeof(BossGeneralState),
            typeof(AttackSensor),
            typeof(StealthEngaging),
            typeof(StealthPreAttackState),
            typeof(StealthEngagingScheme),
        ],
        InlineReferencesBase = [
            typeof(AbstractConditionComp),
        ],
    };

    private static JsonSerializer testSerializer => JsonSerializer.Create(new JsonSerializerSettings {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Error = (_, args) => {
            args.ErrorContext.Handled = true;
            var msg =
                $"Serialization error while creating snapshot: {args.CurrentObject?.GetType()}: {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}";
            Log.Error(msg);
        },
        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        ReferenceResolverProvider = () => referenceResolver,
        ContractResolver = new CustomizableContractResolver {
            // ForceReadableWritable = false,
            PropertyBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            ExactFieldTypesToIgnore = [
                typeof(StateMachine<MonsterBase.States>),
                typeof(MonsterCore),
                typeof(StealthGameMonster),
            ],
            ContainerTypesToIgnore = [
                typeof(Component),
                typeof(MonoBehaviour),
                typeof(Behaviour),
                typeof(Object),
            ],
            FieldAllowlist = new Dictionary<Type, string[]> {
                {
                    typeof(BossGeneralState),
                    [
                        "state", "linkedStateTypes", "linkNextMoveStateWeights", "linkInterruptMoveConditionalWeights",
                        "DelayOffset",
                        "groupingNodes", "ToCloseTransitionState", "HasInterruptTurnaround", "CanInterruptStartTime",
                        "InterruptJumpToTime", "EnterClearSpeed", "ClearSpeedWithDecay", "directionRemap",
                    ]
                }, {
                    typeof(MonsterState),
                    [
                        "clip", "exitState", "HasVariation", "VariationRatio",
                    ]
                },
                { typeof(ActorState), [] },
                { typeof(MappingState), [] },
                { typeof(PlayerSensor), ["triggerDelay", "triggerOnce", "IsOnGroundOnly"] },
                { typeof(OptionWeightQueue<>), ["IsRandom", "stateWeightList", "customizedInitQueue"] }, {
                    typeof(StealthPreAttackState),
                    [
                        "exitPreAttackCoolDown", "ChangeToEngageDelayTime", "ApproachingSchemes", "IsGuardingPath",
                    ]
                }, {
                    typeof(TeleportBinding),
                    ["teleportScheme", "offsetCandidates", "offset", "offsetYFromGround", "PhysicsDetect"]
                },
                { typeof(MonsterStateQueue), ["linkNextMoveStateWeights"] },
            },
            FieldDenylist = new Dictionary<Type, string[]> {
                { typeof(LinkNextMoveStateWeight), ["MustUseEnqueued"] }, {
                    typeof(AttackSensor),
                    [
                        "BindindAttack", "monster", "CoolDownResetReason", "QueuedAttacks",
                        "AttackMoveCount", "_failReason", "_collider", "_cooldownCounter",
                    ]
                },
                { typeof(MonsterHurtInterrupt), ["monster", "currentAccumulateDamage"] },
            },
        },
        Converters = new List<JsonConverter> {
            new TransformConverter(),
            new Vector2Converter(),
            new Vector3Converter(),
            new Vector4Converter(),
            new QuatConverter(),
            new AnimatorConverter(),
            new EnumConverter(),
            // new StringEnumConverter(),
            new ColorConverter(),
            new Color32Converter(),
            new FuncConverter<MonsterStateSequenceWeight, OptionWeightQueue<MonsterStateGroupSequence>>(x => x.setting),
            new FuncConverter<MonsterStateGroup, OptionWeightQueue<MonsterState>>(x => x.setting),
            new FuncConverter<AttackWeight, AttackWeightData>(x => new AttackWeightData {
                StateType = x.StateType,
                Weight = x.weight,
            }),
            new FuncConverter<UnityEvent, string>(x =>
                $"UnityEvent({x.m_Calls.Count})"),
            new FuncConverter<AnimationClip, AnimationEventData[]>(clip => clip.events
                .Select(e => {
                    if (e.functionName != "InvokeAnimationEvent") {
                        return new AnimationEventData {
                            Time = e.time,
                            Event = null,
                        };
                    }

                    return new AnimationEventData {
                        Time = e.time,
                        Event = (AnimationEvents.AnimationEvent)e.intParameter,
                    };
                }).ToArray()),
        },
    });

    private record AnimationEventData {
        public float Time;
        public AnimationEvents.AnimationEvent? Event;
    };

    private record AttackWeightData {
        public MonsterBase.States StateType;
        public int Weight;
    };
}