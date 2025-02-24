using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using DebugModPlus.Modules;
using MonsterLove.StateMachine;
using Newtonsoft.Json.Linq;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus.Savestates;

[Flags]
public enum SavestateFilter {
    None = 0,
    Player = 1 << 1,
    Monsters = 1 << 2,
    Flags = 1 << 3,

    All = Flags | Player | Monsters,
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
        var fsmSnapshots = new List<FsmSnapshot>();
        var referenceFixups = new List<ReferenceFixups>();
        var flagsJson = new JObject();

        // TODO:
        // - jades
        //  - revival jade
        // - qi in UI
        // - broken floor

        var seen = new HashSet<MonoBehaviour>();
        if (filter.HasFlag(SavestateFilter.Player)) {
            MonobehaviourTracing.TraceReferencedMonobehaviours(player, sceneBehaviours, seen, maxDepth: 4);
            foreach (var (_, state) in FsmInspectorModule.FsmListStates(player.fsm)) {
                MonobehaviourTracing.TraceReferencedMonobehaviours(state, sceneBehaviours, seen);
            }
        }

        if (filter.HasFlag(SavestateFilter.Monsters)) {
            foreach (var monster in Object.FindObjectsOfType<MonsterBase>()) {
                MonobehaviourTracing.TraceReferencedMonobehaviours(monster, sceneBehaviours, seen);
                fsmSnapshots.Add(FsmSnapshot.Of(monster.fsm));
            }
        }

        if (filter.HasFlag(SavestateFilter.Player)) {
            fsmSnapshots.Add(FsmSnapshot.Of(player.fsm));
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
            FsmSnapshots = fsmSnapshots,
            ReferenceFixups = referenceFixups,
        };

        return savestate;
    }


    public static async Task Load(Savestate savestate, bool reload = true) {
        if (!GameCore.IsAvailable()) {
            throw new Exception("Attempted to load savestate outside of scene");
        }

        var sw = Stopwatch.StartNew();

        // Load flags
        sw.Start();
        FlagLogic.LoadFlags(savestate.Flags, SaveManager.Instance.allFlags);
        Log.Info($"- Applied flags in {sw.ElapsedMilliseconds}ms");

        // Change scene
        var isCurrentScene = savestate.Scene == (GameCore.Instance.gameLevel is { } x ? x.SceneName : null);
        if (!isCurrentScene || reload) {
            sw.Restart();
            var task = ChangeSceneAsync(new SceneConnectionPoint.ChangeSceneData {
                sceneName = savestate.Scene,
                playerSpawnPosition = () => savestate.PlayerPosition,
            });
            if (await Task.WhenAny(task, Task.Delay(5000)) != task) {
                ToastManager.Toast("Savestate was not loaded after 5s, aborting");
                return;
            }

            Log.Info($"- Change scene in {sw.ElapsedMilliseconds}ms");
        }

        // GameCore.Instance.ResetLevel();

        sw.Restart();
        ApplySnapshots(savestate.MonobehaviourSnapshots);
        Log.Info($"- Apply to scene in {sw.ElapsedMilliseconds}ms");
        sw.Stop();

        ApplyFixups(savestate.ReferenceFixups);

        foreach (var fsm in savestate.FsmSnapshots) {
            var targetGo = ObjectUtils.LookupPath(fsm.Path);
            if (targetGo == null) {
                Log.Error($"Savestate stored fsm state on {fsm.Path}, which does not exist at load time");
                continue;
            }

            var runner = targetGo.GetComponent<FSMStateMachineRunner>();
            if (!runner) {
                Log.Error($"Savestate stored fsm state on {fsm.Path}, which has no FSMStateMachineRunner");
                continue;
            }

            foreach (var machine in FsmInspectorModule.FsmListMachines(runner)) {
                var stateObj = Enum.ToObject(machine.CurrentStateMap.stateObj.GetType(), fsm.CurrentState);

                EnterStateDirectly(machine, stateObj);
            }
        }

        // CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);
        Player.i.playerInput.RevokeAllMyVote(Player.i.PlayerDeadState);
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