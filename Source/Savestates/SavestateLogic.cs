using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DebugModPlus.Utils;
using MonsterLove.StateMachine;
using Newtonsoft.Json.Linq;
using NineSolsAPI;
using NineSolsAPI.Utils;
using PrimeTween;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus.Savestates;

[Flags]
public enum SavestateFilter {
    None = 0,
    Flags = 1 << 1,
    Player = 1 << 2,
    Monsters = 1 << 3,

    // ReSharper disable once InconsistentNaming
    FSMs = 1 << 4,

    All = Flags | Player | FSMs | Monsters,
}

public static class SavestateLogic {
    public static Savestate Create(SavestateFilter filter) {
        if (!GameCore.IsAvailable()) {
            throw new Exception("Can't create savestate outside of game level");
        }

        var gameCore = GameCore.Instance;
        if (!gameCore.gameLevel) {
            throw new Exception("Can't create savestate outside of game level");
        }

        var player = Player.i;

        var sceneBehaviours = new List<MonoBehaviourSnapshot>();
        var monsterLoveFsmSnapshots = new List<MonsterLoveFsmSnapshot>();
        var fsmSnapshots = new List<GeneralFsmSnapshot>();
        var referenceFixups = new List<ReferenceFixups>();
        var flagsJson = new JObject();

        // TODO:
        // - jades
        //  - revival jade
        // - qi in UI
        // - broken floor

        var seen = new HashSet<MonoBehaviour>();
        if (filter.HasFlag(SavestateFilter.Player)) {
            MonobehaviourTracing.TraceReferencedMonobehaviours(player, sceneBehaviours, seen, maxDepth: null);
            foreach (var (_, state) in player.fsm.GetStates()) {
                MonobehaviourTracing.TraceReferencedMonobehaviours(state, sceneBehaviours, seen);
            }
        }

        // sceneBehaviours.Add(MonoBehaviourSnapshot.Of(player.SpriteHolder));

        if (filter.HasFlag(SavestateFilter.Monsters)) {
            foreach (var monster in Object.FindObjectsOfType<MonsterBase>()) {
                MonobehaviourTracing.TraceReferencedMonobehaviours(monster, sceneBehaviours, seen, maxDepth: null);
                monsterLoveFsmSnapshots.Add(MonsterLoveFsmSnapshot.Of(monster.fsm));
                foreach (var (_, state) in monster.fsm.GetStates()) {
                    MonobehaviourTracing.TraceReferencedMonobehaviours(state, sceneBehaviours, seen);
                }
            }
        }

        if (filter.HasFlag(SavestateFilter.FSMs)) {
            foreach (var smo in Object.FindObjectsOfType<StateMachineOwner>()) {
                fsmSnapshots.Add(GeneralFsmSnapshot.Of(smo));
            }
        }

        if (filter.HasFlag(SavestateFilter.Player)) {
            monsterLoveFsmSnapshots.Add(MonsterLoveFsmSnapshot.Of(player.fsm));
            referenceFixups.Add(ReferenceFixups.Of(Player.i,
            [
                new ReferenceFixupField(nameof(Player.i.touchingRope),
                    ObjectUtils.ObjectComponentPath(Player.i.touchingRope)),
            ]));
        }

        if (filter.HasFlag(SavestateFilter.Flags)) {
            // PERF: remove parse(encode(val))
#pragma warning disable CS0618 // Type or member is obsolete
            flagsJson = JObject.Parse(GameFlagManager.FlagsToJson(SaveManager.Instance.allFlags));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        var savestate = new Savestate {
            Flags = flagsJson,
            Scene = gameCore.gameLevel.gameObject.scene.name,
            PlayerPosition = player.transform.position,
            LastTeleportId = ApplicationCore.Instance.lastSaveTeleportPoint.FinalSaveID,
            MonobehaviourSnapshots = sceneBehaviours,
            FsmSnapshots = monsterLoveFsmSnapshots,
            GeneralFsmSnapshots = fsmSnapshots,
            ReferenceFixups = referenceFixups,
        };

        return savestate;
    }


    public static async Task Load(Savestate savestate, bool forceReload = false) {
        if (!GameCore.IsAvailable()) {
            throw new Exception("Attempted to load savestate outside of scene");
        }

        if (savestate.LastTeleportId != null) {
            var tp = GameFlagManager.Instance.GetTeleportPointWithPath(savestate.LastTeleportId);
            ApplicationCore.Instance.lastSaveTeleportPoint = tp;
        }


        var sw = Stopwatch.StartNew();

        // Load flags
        sw.Start();
        if (savestate.Flags is { } flags) {
            FlagLogic.LoadFlags(flags, SaveManager.Instance.allFlags);
            Log.Debug($"- Applied flags in {sw.ElapsedMilliseconds}ms");

            SaveManager.Instance.allFlags.AllFlagInitStartAndEquip();
        }

        // Change scene
        var isCurrentScene = savestate.Scene == (GameCore.Instance.gameLevel is { } x ? x.SceneName : null);
        if (savestate.Scene != null) {
            if ((savestate.Scene != null && !isCurrentScene) || forceReload) {
                if (savestate.PlayerPosition is not { } playerPosition) {
                    throw new Exception("Savestate with scene must have `playerPosition`");
                }

                sw.Restart();
                var task = ChangeSceneAsync(new SceneConnectionPoint.ChangeSceneData {
                    sceneName = savestate.Scene,
                    playerSpawnPosition = () => playerPosition,
                });
                if (await Task.WhenAny(task, Task.Delay(5000)) != task) {
                    ToastManager.Toast("Savestate was not loaded after 5s, aborting");
                    return;
                }

                Log.Info($"- Change scene in {sw.ElapsedMilliseconds}ms");
            }
        } else {
            if (savestate.PlayerPosition is { } playerPosition) {
                Player.i.transform.position = playerPosition;
            }
        }

        GameCore.Instance.ResetLevel();

        sw.Restart();
        if (savestate.MonobehaviourSnapshots != null) {
            ApplySnapshots(savestate.MonobehaviourSnapshots);
            Log.Info($"- Applied snapshots to scene in {sw.ElapsedMilliseconds}ms");
        }

        sw.Stop();

        if (savestate.ReferenceFixups != null) {
            ApplyFixups(savestate.ReferenceFixups);
        }

        foreach (var fsm in savestate.FsmSnapshots ?? new List<MonsterLoveFsmSnapshot>()) {
            var targetGo = ObjectUtils.LookupPath(fsm.Path);
            if (targetGo == null) {
                Log.Error($"Savestate stored monsterlove fsm state on {fsm.Path}, which does not exist at load time");
                continue;
            }

            var runner = targetGo.GetComponent<FSMStateMachineRunner>();
            if (!runner) {
                Log.Error($"Savestate stored monsterlove fsm state on {fsm.Path}, which has no FSMStateMachineRunner");
                continue;
            }

            foreach (var machine in runner.GetMachines()) {
                var stateObj = Enum.ToObject(machine.CurrentStateMap.stateObj.GetType(), fsm.CurrentState);

                EnterStateDirectly(machine, stateObj);
            }
        }

        foreach (var fsm in savestate?.GeneralFsmSnapshots ?? new List<GeneralFsmSnapshot>()) {
            var targetGo = ObjectUtils.LookupPath(fsm.Path);
            if (targetGo == null) {
                Log.Error($"Savestate stored general fsm state on {fsm.Path}, which does not exist at load time");
                continue;
            }

            var owner = targetGo.GetComponent<StateMachineOwner>();
            if (!owner) {
                Log.Error($"Savestate stored general fsm state on {fsm.Path}, which has no FSMStateMachineRunner");
                continue;
            }

            var state = owner.FsmContext.States.FirstOrDefault(state => state.name == fsm.CurrentState);
            if (!state) {
                Log.Error($"State {fsm.CurrentState} does not exist on {fsm.Path}");
                continue;
            }

            try {
                owner.FsmContext.ChangeState(state);
            } catch (Exception e) {
                Log.Error($"Could not apply fsm state on {owner.FsmContext}/{owner.FsmContext.fsm} {e}");
            }
        }

        // CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);
        // hacks
        Player.i.playerInput.RevokeAllMyVote(Player.i.PlayerDeadState);
        Tween.StopAll(); // should restore as well
        foreach (var bossArea in Object.FindObjectsOfType<BossArea>()) {
            bossArea.ForceShowHP();
        }

        var votes = Player.i.playerInput.AccessField<List<RuntimeConditionVote>>("conditionVoteList");
        foreach (var vote in votes) {
            vote.votes.Clear();
            vote.ManualUpdate();
        }

        Player.i.UpdateSpriteFacing();
    }

    private static void ApplySnapshots(List<MonoBehaviourSnapshot> snapshots) {
        foreach (var mb in snapshots) {
            var targetComponent = ObjectUtils.LookupObjectComponentPath(mb.Path);
            if (targetComponent == null) {
                Log.Error($"Savestate stored state on {mb.Path}, which does not exist at load time");
                continue;
            }

            SnapshotSerializer.Populate(targetComponent, mb.Data);
        }
    }

    private static void ApplyFixups(List<ReferenceFixups> fixups) {
        foreach (var fields in fixups) {
            var targetComponent = ObjectUtils.LookupObjectComponentPath(fields.Path);
            if (targetComponent == null) {
                Log.Error($"Savestate stored reference fixup on {fields.Path}, which does not exist at load time");
                continue;
            }

            foreach (var (fieldName, referencedPath) in fields.Fields) {
                var field = targetComponent.GetType().GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
                if (referencedPath == null) {
                    field.SetValue(targetComponent, null);
                } else {
                    var referencedObject = ObjectUtils.LookupObjectComponentPath(referencedPath);
                    if (referencedObject == null) {
                        Log.Error(
                            $"Savestate stored reference fixup on {fields.Path}.{fieldName}, but the target {referencedPath} does not exist at load time");
                        continue;
                    }

                    field.SetValue(targetComponent, referencedObject);
                }
            }
        }
    }


    private static void EnterStateDirectly(IStateMachine sm, object stateObj) {
        // TODO: handle transitions

        var engine = sm.AccessField<FSMStateMachineRunner>("engine");
        var stateLookup = sm.AccessField<IDictionary>("stateLookup");
        if (!stateLookup.Contains(stateObj)) {
            throw new Exception($"state {stateObj} not found in fsm");
        }

        var newStateMapping = stateLookup[stateObj];

        var queuedChangeField = sm.AccessFieldInfo("queuedChange");
        var currentTransitionField = sm.AccessFieldInfo("currentTransition");
        var exitRoutineField = sm.AccessFieldInfo("exitRoutine");
        var enterRoutineField = sm.AccessFieldInfo("enterRoutine");
        var lastStateField = sm.AccessFieldInfo("lastState");
        var currentStateField = sm.AccessFieldInfo("currentState");
        var isInTransitionField = sm.AccessFieldInfo("isInTransition");

        if (queuedChangeField.GetValue(sm) is IEnumerator queuedChange) {
            engine.StopCoroutine(queuedChange);
            queuedChangeField.SetValue(sm, null);
        }

        if (currentTransitionField.GetValue(sm) is IEnumerator currentTransition) {
            engine.StopCoroutine(currentTransition);
            currentTransitionField.SetValue(sm, null);
        }

        if (exitRoutineField.GetValue(sm) is IEnumerator exitRoutine) {
            engine.StopCoroutine(exitRoutine);
            exitRoutineField.SetValue(sm, null);
        }

        if (enterRoutineField.GetValue(sm) is IEnumerator enterRoutine) {
            engine.StopCoroutine(enterRoutine);
            enterRoutineField.SetValue(sm, null);
        }

        lastStateField.SetValue(sm, newStateMapping);
        currentStateField.SetValue(sm, newStateMapping);
        isInTransitionField.SetValue(sm, false);
    }


    private static Task ChangeSceneAsync(SceneConnectionPoint.ChangeSceneData changeSceneData, bool showTip = false) {
        var completion = new TaskCompletionSource<object?>();
        changeSceneData.ChangedDoneEvent = () => completion.SetResult(null);
        GameCore.Instance.ChangeSceneCompat(changeSceneData, showTip);

        return completion.Task;
    }
}