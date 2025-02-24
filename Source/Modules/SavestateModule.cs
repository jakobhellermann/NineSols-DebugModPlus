using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus.Modules;

[Flags]
public enum SavestateFilter {
    None = 0,
    Player = 1 << 1,
    Monsters = 1 << 2,
    Flags = 1 << 3,

    All = Flags | Player | Monsters,
}

internal class Savestate {
    public required string Scene;
    public Vector3 PlayerPosition;
    public required string LastTeleportId;
    public required List<MonoBehaviourSnapshot> MonobehaviourSnapshots;
    public required List<FsmSnapshot> FsmSnapshots;
    public required List<ReferenceFixups> ReferenceFixups;
    public required JObject Flags;

    public void SerializeTo(StreamWriter writer) {
        JsonSerializer.Create(jsonSettings).Serialize(writer, this);
    }

    public static Savestate DeserializeFrom(StreamReader reader) {
        var jsonReader = new JsonTextReader(reader);
        return JsonSerializer.Create(jsonSettings).Deserialize<Savestate>(jsonReader) ??
               throw new Exception("Failed to deserialize savestate");
    }

    public string Serialize() {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    public static Savestate Deserialize(string data) {
        return JsonConvert.DeserializeObject<Savestate>(data) ?? throw new Exception("Failed to deserialize savestate");
    }

    private static JsonSerializerSettings jsonSettings = new JsonSerializerSettings {
        Formatting = Formatting.Indented,
        Converters = [new Vector3Converter()],
    };
}

class SavestateCollection {
    private string? backingDir;
    private string BackingDir => backingDir ??= ModDirs.DataDir(DebugModPlus.Instance, "Savestates");

    private string SavestatePath(string slot) {
        return Path.Join(BackingDir, $"{slot}.json");
    }

    public void Save(string slot, Savestate savestate) {
        var path = SavestatePath(slot);
        try {
            var sw = Stopwatch.StartNew();
            using var file = File.CreateText(path);
            savestate.SerializeTo(file);
            Log.Info($"Saving state took {sw.ElapsedMilliseconds}ms");
        } catch (Exception) {
            File.Delete(path);
            throw;
        }
    }

    public bool TryGetValue(string slot, [NotNullWhen(true)] out Savestate? savestate) {
        var path = SavestatePath(slot);
        try {
            var sw = Stopwatch.StartNew();
            using var reader = File.OpenText(path);
            savestate = Savestate.DeserializeFrom(reader);
            Log.Info($"- Reading state from disk {sw.ElapsedMilliseconds}ms");
            return true;
        } catch (FileNotFoundException) {
            savestate = null;
            return false;
        }
    }
}

internal class FsmSnapshot {
    public required string Path;
    public required object CurrentState;

    public static FsmSnapshot Of(IStateMachine machine) => new() {
        Path = ObjectUtils.ObjectPath(machine.Component.gameObject),
        CurrentState = machine.CurrentStateMap.stateObj,
    };
}

internal class MonoBehaviourSnapshot {
    public required string Path;
    public required JToken Data;

    public static MonoBehaviourSnapshot Of(MonoBehaviour mb) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Data = SnapshotSerializer.Snapshot(mb),
    };
}

internal record ReferenceFixupField(string Field, string? Reference);

internal class ReferenceFixups {
    public required string Path;
    public required List<ReferenceFixupField> Fields;

    public static ReferenceFixups Of(MonoBehaviour mb, List<ReferenceFixupField> fixups) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Fields = fixups,
    };
}

public class SavestateModule(ConfigEntry<SavestateFilter> currentFilter) {
    public static bool IsLoadingSavestate;

    public event EventHandler? SavestateLoaded;
    public event EventHandler? SavestateCreated;

    private SavestateCollection savestates = new();

    #region Monobehaviour State Tracking

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

    private static void SnapshotReferencedMonoBehaviours(
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

            SnapshotReferencedMonoBehaviours(value, saved, seen, depth + 1, maxDepth, minDepth);
        }
    }

    #endregion

    #region Entrypoints

    [BindableMethod(Name = "Create Savestate")]
    private static void CreateSavestateMethod() {
        const string slot = "main";
        DebugModPlus.Instance.SavestateModule.TryCreateSavestate(slot);
    }

    public void TryCreateSavestate(string slot) {
        try {
            var sw = Stopwatch.StartNew();
            CreateSavestate(slot, currentFilter.Value);
            Log.Info($"Created savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e.Message);
            return;
        }

        ToastManager.Toast($"Savestate {slot} created");
    }

    public async void TryLoadSavestate(string slot, bool reload = false) {
        if (!savestates.TryGetValue(slot, out var savestate)) {
            ToastManager.Toast($"Savestate '{slot}' not found");
            return;
        }

        try {
            var sw = Stopwatch.StartNew();
            if (await LoadSavestate(savestate, reload)) {
                Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
            }
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }


    [BindableMethod(Name = "Load Savestate")]
    private static void LoadSavestateMethod() {
        const string slot = "main";
        DebugModPlus.Instance.SavestateModule.TryLoadSavestate(slot, true);
    }

    [BindableMethod(Name = "Load Savestate\n(No reload)")]
    private static void LoadSavestateMethodNoReload() {
        const string slot = "main";
        DebugModPlus.Instance.SavestateModule.TryLoadSavestate(slot);
    }

    #endregion

    #region Create Savestate

    private void CreateSavestate(string slot, SavestateFilter filter) {
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
            SnapshotReferencedMonoBehaviours(player, sceneBehaviours, seen, maxDepth: 4);
            foreach (var (_, state) in FsmInspectorModule.FsmListStates(player.fsm)) {
                SnapshotReferencedMonoBehaviours(state, sceneBehaviours, seen);
            }
        }

        if (filter.HasFlag(SavestateFilter.Monsters)) {
            foreach (var monster in Object.FindObjectsOfType<MonsterBase>()) {
                SnapshotReferencedMonoBehaviours(monster, sceneBehaviours, seen);
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

        try {
            savestates.Save(slot, savestate);
        } catch (Exception e) {
            ToastManager.Toast($"Could not persist savestate to disk: {e.Message}");
            Log.Error(e);
        }

        SavestateCreated?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Load Savestate

    private static void LoadDebugSave() {
        SaveManager.Instance.LoadSaveAtSlot(100);
        ApplicationUIGroupManager.Instance.ClearAll();
        RuntimeInitHandler.LoadCore();

        if (!GameVersions.IsVersion(GameVersions.SpeedrunPatch)) {
            typeof(GameConfig).GetMethod("InstantiateGameCore")!.Invoke(GameConfig.Instance, []);
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private async Task<bool> LoadSavestate(Savestate savestate, bool reload = true) {
        if (IsLoadingSavestate) {
            Log.Error("Attempted to load savestate while loading savestate");
            return false;
        }

        try {
            IsLoadingSavestate = true;
            return await LoadSavestateInner(savestate, reload);
        } finally {
            IsLoadingSavestate = false;
        }
    }


    private async Task<bool> LoadSavestateInner(Savestate savestate, bool reload = true) {
        if (!GameCore.IsAvailable()) {
            LoadDebugSave();
            var tp = GameFlagManager.Instance.GetTeleportPointWithPath(savestate.LastTeleportId);
            ApplicationCore.Instance.lastSaveTeleportPoint = tp;
            await ApplicationCore.Instance.ChangeSceneCompat(tp.sceneName);
            // TODO: figure out what to wait for
            await UniTask.DelayFrame(10);
        }

        var sw = Stopwatch.StartNew();

        // Load flags
        // saveManager.allStatData.ClearStats();
        sw.Start();
        LoadFlags(savestate.Flags, SaveManager.Instance.allFlags);
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
                return false;
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

        //CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);

        SavestateLoaded?.Invoke(this, EventArgs.Empty);

        return true;
    }

    void EnterStateDirectly(IStateMachine sm, object stateObj) {
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


    private static Task ChangeSceneAsync(SceneConnectionPoint.ChangeSceneData changeSceneData, bool showTip = false) {
        var completion = new TaskCompletionSource<object?>();
        changeSceneData.ChangedDoneEvent = () => completion.SetResult(null);
        GameCore.Instance.ChangeSceneCompat(changeSceneData, showTip);

        return completion.Task;
    }

    #endregion

    #region Flag Load/Save

    private static void LoadFlags(JObject newFlags, GameFlagCollection allFlags) {
        foreach (var keyValuePair in allFlags.flagDict) {
            var (name, gameFlagBase2) = keyValuePair;

            if (newFlags[name] is not JObject newField) continue;

            foreach (var (key, flagField) in gameFlagBase2.fieldCaches) {
                var jValue = newField[key];
                if (jValue == null) continue;

                switch (flagField) {
                    case FlagFieldBool flagFieldBool:
                        flagFieldBool.CurrentValue = jValue.Value<bool>();
                        break;
                    case FlagFieldInt flagFieldInt:
                        flagFieldInt.CurrentValue = jValue.Value<int>();
                        break;
                    case FlagFieldString flagFieldString:
                        flagFieldString.CurrentValue = jValue.Value<string>();
                        break;
                    case FlagFieldFloat flagFieldFloat:
                        flagFieldFloat.CurrentValue = jValue.Value<float>();
                        break;
                    case FlagFieldLong flagFieldLong:
                        flagFieldLong.CurrentValue = jValue.Value<long>();
                        break;
                }
            }
        }

        #endregion
    }
}