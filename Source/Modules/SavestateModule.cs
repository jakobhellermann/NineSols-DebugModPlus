using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DebugModPlus.Modules;

internal class Savestate {
    public required string Scene;
    public Vector3 PlayerPosition;
    public required List<MonoBehaviourSnapshot> SceneBehaviours;
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
    private string BackingDir => backingDir ??= SavestateModule.ModDataDir(DebugModPlus.Instance, "Savestates");

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

internal class MonoBehaviourSnapshot {
    public required string Path;
    public required string Component;
    public required string Type;
    public required JToken Data;

    public static MonoBehaviourSnapshot Of(MonoBehaviour mb) => new() {
        Path = ObjectUtils.ObjectPath(mb.gameObject),
        Component = mb.name,
        Type = mb.GetType().Name,
        Data = SnapshotSerializer.Snapshot(mb),
    };
}

public class SavestateModule {
    public static bool IsLoadingSavestate;

    public event EventHandler? SavestateLoaded;
    public event EventHandler? SavestateCreated;

    private SavestateCollection savestates = new();

    public void Unload() {
    }

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

    public static string ModDataDir(BaseUnityPlugin mod, params string[] subDirs) =>
        ModDataDir(mod.Info.Metadata.GUID, subDirs);

    private static string ModDataDir(string modGuid, params string[] subDirs) {
        var gameDir = Directory.GetParent(Application.dataPath);
        if (gameDir == null) {
            throw new Exception($"{Application.dataPath} is not a valid game directory?");
        }

        var folder = subDirs.Aggregate(Path.Combine(gameDir.FullName, "ModData", modGuid), Path.Combine);
        Directory.CreateDirectory(folder);
        return folder;
    }

    #region Entrypoints

    [BindableMethod(Name = "Create Savestate")]
    private static void CreateSavestateMethod() {
        const string slot = "main";
        DebugModPlus.Instance.SavestateModule.TryCreateSavestate(slot);
    }

    public void TryCreateSavestate(string slot) {
        try {
            var sw = Stopwatch.StartNew();
            CreateSavestate(slot);
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

    private void CreateSavestate(string slot) {
        if (!GameCore.IsAvailable()) {
            throw new Exception("Can't create savestate outside of game level");
        }

        var gameCore = GameCore.Instance;
        if (!gameCore.gameLevel) {
            throw new Exception("Can't create savestate outside of game level");
        }

        var player = Player.i;
        var currentPos = player.transform.position;

        var sceneBehaviours = new List<MonoBehaviourSnapshot>();
        {
            var seen = new HashSet<MonoBehaviour>();

            // TODO:
            // - jades
            //  - revival jade
            // - qi in UI
            // - broken floor

            SnapshotReferencedMonoBehaviours(player, sceneBehaviours, seen, maxDepth: 4);
            foreach (var (_, state) in FsmInspectorModule.FsmListStates(player.fsm)) {
                SnapshotReferencedMonoBehaviours(state, sceneBehaviours, seen);
            }
        }

        var flagsJson = new JObject();

        // PERF: remove parse(encode(val))
        // var flagsJson = JObject.Parse(GameFlagManager.FlagsToJson(SaveManager.Instance.allFlags));

        var savestate = new Savestate {
            Flags = flagsJson,
            Scene = gameCore.gameLevel.gameObject.scene.name,
            PlayerPosition = currentPos,
            SceneBehaviours = sceneBehaviours,
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
            // await ApplicationCore.Instance.ChangeSceneCompat(savestate.Scene);
            await ApplicationCore.Instance.StartGameGoTo(GameFlagManager.Instance.startPoint);
            await UniTask.DelayFrame(10);
        }

        var sw = Stopwatch.StartNew();

        // Load flags
        // saveManager.allStatData.ClearStats();
        sw.Start();
        LoadFlags(savestate.Flags, SaveManager.Instance.allFlags);
        Log.Info($"- Applied flags in {sw.ElapsedMilliseconds}ms");

        // Change scene
        var isCurrentScene = savestate.Scene == GameCore.Instance.gameLevel?.SceneName;
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
        ApplySnapshots(savestate.SceneBehaviours);
        Log.Info($"- Apply to scene in {sw.ElapsedMilliseconds}ms");
        sw.Stop();

        //CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);

        SavestateLoaded?.Invoke(this, EventArgs.Empty);

        return true;
    }

    private static void ApplySnapshots(List<MonoBehaviourSnapshot> snapshots) {
        List<MonoBehaviour> components = new();

        foreach (var mb in snapshots) {
            var targetGo = DebugModPlus.LookupPath(mb.Path);
            if (targetGo == null) {
                Log.Error($"Savestate stored state on {mb.Path}, which does not exist at load time");
                continue;
            }

            targetGo.GetComponents(components);
            var targetComponent = components.Find(component => component.name == mb.Component);
            if (targetComponent == null) {
                Log.Error(
                    $"Savestate stored state on {mb.Path}.{mb.Component}, but the component does not exist at load time");
                continue;
            }

            components.Clear();

            Log.Debug($"Applying into {mb.Path}@{mb.Component}");
            SnapshotSerializer.Populate(targetComponent, mb.Data);
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