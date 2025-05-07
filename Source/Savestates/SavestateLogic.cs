using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DebugModPlus.Interop;
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
// when updating, also update DebugModPlusInteropGlue in TAS Tools

[Flags]
public enum SavestateLoadMode {
    None = 0,
    ResetScene = 1 << 0,
    ReloadScene = 1 << 1,
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

        var sceneBehaviours = new List<ComponentSnapshot>();
        var gameObjects = new List<GameObjectSnapshot>();
        var monsterLoveFsmSnapshots = new List<MonsterLoveFsmSnapshot>();
        var fsmSnapshots = new List<GeneralFsmSnapshot>();
        var flagsJson = new JObject();

        // TODO:
        // - jades
        //  - revival jade
        // - qi in UI
        // - broken floor

        var seen = new HashSet<Component>();

        if (filter.HasFlag(SavestateFilter.Player)) {
            sceneBehaviours.Add(ComponentSnapshot.Of(player.transform));
            gameObjects.Add(GameObjectSnapshot.Of(player.pushAwayCollider.gameObject));
            SnapshotSerializer.SnapshotRecursive(player, sceneBehaviours, seen);
            foreach (var (_, state) in player.fsm.GetStates()) {
                SnapshotSerializer.SnapshotRecursive(state, sceneBehaviours, seen, 0);
            }

            SnapshotSerializer.SnapshotRecursive(CameraManager.Instance.camera2D, sceneBehaviours, seen, 0);
        }

        if (filter.HasFlag(SavestateFilter.Monsters)) {
            foreach (var monster in Object.FindObjectsOfType<MonsterBase>()) {
                sceneBehaviours.Add(ComponentSnapshot.Of(monster.transform));
                SnapshotSerializer.SnapshotRecursive(monster, sceneBehaviours, seen);
                if (monster.fsm == null) {
                    Log.Warning($"{monster} fsm was null, skipping");
                    continue;
                }

                monsterLoveFsmSnapshots.Add(MonsterLoveFsmSnapshot.Of(monster.fsm));

                foreach (var attackSensor in monster.AttackSensorsCompat()) {
                    SnapshotSerializer.SnapshotRecursive(attackSensor, sceneBehaviours, seen);
                }

                foreach (var (_, state) in monster.fsm.GetStates()) {
                    SnapshotSerializer.SnapshotRecursive(state, sceneBehaviours, seen);
                }

                monsterLoveFsmSnapshots.Add(MonsterLoveFsmSnapshot.Of(monster.fsm));
            }
        }

        if (filter.HasFlag(SavestateFilter.FSMs)) {
            foreach (var smo in Object.FindObjectsOfType<StateMachineOwner>()) {
                if (!smo.FsmContext?.fsm?.State) {
                    Log.Warning($"{smo} fsm was null, skipping");
                    continue;
                }

                fsmSnapshots.Add(GeneralFsmSnapshot.Of(smo));
            }
        }

        if (filter.HasFlag(SavestateFilter.Player)) {
            monsterLoveFsmSnapshots.Add(MonsterLoveFsmSnapshot.Of(player.fsm));
        }

        if (filter.HasFlag(SavestateFilter.Flags)) {
            // PERF: remove parse(encode(val))
#pragma warning disable CS0618 // Type or member is obsolete
            flagsJson = JObject.Parse(GameFlagManager.FlagsToJson(SaveManager.Instance.allFlags));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        var savestate = new Savestate {
            Flags = flagsJson.Count == 0 ? null : flagsJson,
            Scene = gameCore.gameLevel.gameObject.scene.name,
            PlayerPosition = filter.HasFlag(SavestateFilter.Player) ? player.transform.position : null,
            LastTeleportId = ApplicationCore.Instance.lastSaveTeleportPoint.FinalSaveID,
            MonobehaviourSnapshots = sceneBehaviours,
            GameObjectSnapshots = gameObjects,
            FsmSnapshots = monsterLoveFsmSnapshots.Count == 0 ? null : monsterLoveFsmSnapshots,
            GeneralFsmSnapshots = fsmSnapshots.Count == 0 ? null : fsmSnapshots,
            RandomState = UnityEngine.Random.state,
        };

        return savestate;
    }


    public static async Task Load(Savestate savestate, SavestateLoadMode loadMode) {
        try {
            DebugModPlusInterop.IsLoadingSavestate = true;
            await LoadInner(savestate, loadMode);
        } finally {
            DebugModPlusInterop.IsLoadingSavestate = false;
        }
    }

    private static async Task LoadInner(Savestate savestate, SavestateLoadMode loadMode) {
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

        // Close dialogue
        if (DialoguePlayer.Instance.CanSkip) DialoguePlayer.Instance.ForceClose();

        // Change scene
        var isCurrentScene = savestate.Scene == (GameCore.Instance.gameLevel is { } x ? x.SceneName : null);
        if (savestate.Scene != null) {
            if ((savestate.Scene != null && !isCurrentScene) || loadMode.HasFlag(SavestateLoadMode.ReloadScene)) {
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

        if (loadMode.HasFlag(SavestateLoadMode.ResetScene)) {
            GameCore.Instance.ResetLevel();
        }

        sw.Restart();
        if (savestate.MonobehaviourSnapshots != null) {
            foreach (var mb in savestate.MonobehaviourSnapshots) {
                mb.Restore();
            }

            Log.Info($"- Applied snapshots to scene in {sw.ElapsedMilliseconds}ms");
        }

        sw.Stop();

        foreach (var go in savestate.GameObjectSnapshots ?? []) {
            go.Restore();
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

        foreach (var fsm in savestate.GeneralFsmSnapshots ?? new List<GeneralFsmSnapshot>()) {
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

        foreach (var monster in MonsterManager.Instance.monsterDict.Values) {
            monster.postureSystem.ShowHpViewCheck();
        }

        var votes = Player.i.playerInput.GetFieldValue<List<RuntimeConditionVote>>("conditionVoteList")!;
        foreach (var vote in votes) {
            vote.votes.Clear();
            vote.ManualUpdate();
        }

        if (savestate.RandomState is { } randomState) {
            UnityEngine.Random.state = randomState;
        }

        Player.i.UpdateSpriteFacing();
    }

    private static void EnterStateDirectly(IStateMachine sm, object stateObj) {
        // TODO: handle transitions

        var engine = sm.GetFieldValue<FSMStateMachineRunner>("engine")!;
        var stateLookup = sm.GetFieldValue<IDictionary>("stateLookup")!;
        if (!stateLookup.Contains(stateObj)) {
            throw new Exception($"state {stateObj} not found in fsm");
        }

        var newStateMapping = stateLookup[stateObj];

        var ty = sm.GetType();
        var queuedChangeField = ty.GetFieldInfo("queuedChange")!;
        var currentTransitionField = ty.GetFieldInfo("currentTransition")!;
        var exitRoutineField = ty.GetFieldInfo("exitRoutine")!;
        var enterRoutineField = ty.GetFieldInfo("enterRoutine")!;
        var lastStateField = ty.GetFieldInfo("lastState")!;
        var currentStateField = ty.GetFieldInfo("currentState")!;
        var isInTransitionField = ty.GetFieldInfo("isInTransition")!;

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