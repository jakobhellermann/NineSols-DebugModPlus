using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using DebugModPlus.Savestates;
using NineSolsAPI;

namespace DebugModPlus.Modules;

public class SavestateModule(ConfigEntry<SavestateFilter> currentFilter) {
    public static bool IsLoadingSavestate;

    public event EventHandler? SavestateLoaded;
    public event EventHandler? SavestateCreated;

    private SavestateStore savestates = new();

    #region Entrypoints

    [BindableMethod(Name = "Create Savestate")]
    private static void CreateSavestateMethod() {
        const string slot = "main";
        DebugModPlus.Instance.SavestateModule.TryCreateSavestate(slot);
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
            await LoadSavestate(savestate, reload);
            Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    #endregion

    private void CreateSavestate(string slot, SavestateFilter filter) {
        var savestate = SavestateLogic.Create(filter);

        try {
            savestates.Save(slot, savestate);
        } catch (Exception e) {
            ToastManager.Toast($"Could not persist savestate to disk: {e.Message}");
            Log.Error(e);
        }

        SavestateCreated?.Invoke(this, EventArgs.Empty);
    }

    private static void LoadDebugSave() {
        SaveManager.Instance.LoadSaveAtSlot(100);
        ApplicationUIGroupManager.Instance.ClearAll();
        RuntimeInitHandler.LoadCore();

        if (!GameVersions.IsVersion(GameVersions.SpeedrunPatch)) {
            typeof(GameConfig).GetMethod("InstantiateGameCore")!.Invoke(GameConfig.Instance, []);
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private async Task LoadSavestate(Savestate savestate, bool reload = true) {
        if (IsLoadingSavestate) {
            Log.Error("Attempted to load savestate while loading savestate");
            return;
        }

        try {
            IsLoadingSavestate = true;
            await LoadSavestateInner(savestate, reload);
        } finally {
            IsLoadingSavestate = false;
        }
    }


    private async Task LoadSavestateInner(Savestate savestate, bool reload = true) {
        if (!GameCore.IsAvailable()) {
            LoadDebugSave();
            var tp = GameFlagManager.Instance.GetTeleportPointWithPath(savestate.LastTeleportId);
            ApplicationCore.Instance.lastSaveTeleportPoint = tp;
            await ApplicationCore.Instance.ChangeSceneCompat(tp.sceneName);
            // TODO: figure out what to wait for
            await UniTask.DelayFrame(10);
        }

        await SavestateLogic.Load(savestate, reload);
        SavestateLoaded?.Invoke(this, EventArgs.Empty);
    }
}