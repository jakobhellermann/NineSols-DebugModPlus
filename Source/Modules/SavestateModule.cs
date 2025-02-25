using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Cysharp.Threading.Tasks;
using DebugModPlus.Savestates;
using NineSolsAPI;
using UnityEngine;

namespace DebugModPlus.Modules;

public class SavestateModule(
    ConfigEntry<SavestateFilter> currentFilter,
    ConfigEntry<KeyboardShortcut> openSave,
    ConfigEntry<KeyboardShortcut> openLoad,
    ConfigEntry<KeyboardShortcut> openDelete,
    ConfigEntry<KeyboardShortcut> tabNext,
    ConfigEntry<KeyboardShortcut> tabPrev
) {
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
    private async Task LoadSavestate(Savestate savestate, bool reload = false) {
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

    // 


    private void SaveToSlot(int slot) {
        try {
            var sw = Stopwatch.StartNew();

            var scene = GameCore.Instance.gameLevel.SceneName;
            var defaultName = $"{scene} {DateTime.Now:yyyy-mMM-dd HH-mm-ss}";


            var savestate = SavestateLogic.Create(currentFilter.Value);
            savestates.Save(slot, defaultName, savestate);
            Log.Info($"Created savestate {slot}-{defaultName} in {sw.ElapsedMilliseconds}ms");
            ToastManager.Toast($"Savestate {slot}-{defaultName} created");
        } catch (Exception e) {
            ToastManager.Toast(e.Message);
        }
    }

    private async void LoadFromSlot(int slot) {
        var bySlot = savestates.List(slot).ToList();
        switch (bySlot.Count) {
            case 0:
                ToastManager.Toast($"Savestate '{slot}' not found");
                return;
            case > 1:
                ToastManager.Toast($"Multiple savestates found at slot {slot}, picking {bySlot[0].FullName}");
                break;
        }

        if (!savestates.TryGetValue(bySlot[0], out var savestate)) {
            return;
        }

        try {
            var sw = Stopwatch.StartNew();
            await LoadSavestate(savestate);
            Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    private enum SavestateUIState {
        Off,
        Save,
        Load,
        Delete,
    }

    private SavestateUIState uiState = SavestateUIState.Off;

    private Dictionary<int, SavestateInfo> infos = [];

    private void LoadInfos() {
        infos.Clear();
        foreach (var info in savestates.List()) {
            if (info.index is not { } index) continue;

            infos.TryAdd(index, info);
        }
    }

    public void Update() {
        try {
            if (KeybindManager.CheckShortcutOnly(openLoad.Value)) {
                uiState = uiState == SavestateUIState.Load ? SavestateUIState.Off : SavestateUIState.Load;
            } else if (KeybindManager.CheckShortcutOnly(openSave.Value)) {
                uiState = uiState == SavestateUIState.Save ? SavestateUIState.Off : SavestateUIState.Save;
            } else if (KeybindManager.CheckShortcutOnly(openDelete.Value)) {
                uiState = uiState == SavestateUIState.Delete ? SavestateUIState.Off : SavestateUIState.Delete;
            } else if (KeybindManager.CheckShortcutOnly(tabNext.Value)) {
                currentPage++;
            } else if (KeybindManager.CheckShortcutOnly(tabPrev.Value)) {
                currentPage = Math.Max(currentPage - 1, 0);
            }

            if (uiState != SavestateUIState.Off) {
                LoadInfos();
            }

            if (uiState != SavestateUIState.Off) {
                if (Input.GetKeyDown(KeyCode.Escape)) uiState = SavestateUIState.Off;

                for (var i = 0; i < 10; i++) {
                    if (!Input.GetKeyDown(KeyCode.Alpha0 + i)
                        && !Input.GetKeyDown(KeyCode.Keypad0 + i)) continue;

                    var saveIndex = currentPage * ItemsPerPage + i;

                    if (uiState == SavestateUIState.Save) {
                        SaveToSlot(saveIndex);
                        uiState = SavestateUIState.Off;
                    } else if (uiState == SavestateUIState.Load) {
                        LoadFromSlot(saveIndex);
                        uiState = SavestateUIState.Off;
                    } else if (uiState == SavestateUIState.Delete) {
                        savestates.Delete(saveIndex);
                        uiState = SavestateUIState.Off;
                    }
                }
            }
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    private const int ItemsPerPage = 10;
    private int currentPage = 0;

    public void OnGui() {
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = false };
        styleBox ??= new GUIStyle(GUI.skin.box) { fontSize = 18 };

        if (uiState != SavestateUIState.Off) {
            var maxSlotIndex = infos.Count > 0 ? infos.Max(kv => kv.Key) : 0;
            var totalPages = Math.Max(Mathf.CeilToInt((float)maxSlotIndex / ItemsPerPage), currentPage + 1);

            const int itemHeight = 27;
            var visibleItems = Mathf.Max(ItemsPerPage, infos.Count);
            var boxHeight = (visibleItems + 2) * itemHeight;

            const int boxWidth = 450;
            const int boxInset = 10;
            var boxX = Screen.width / 2 - boxWidth / 2;
            const int boxY = 50;

            var boxRect = new Rect(boxX, boxY, boxWidth, boxHeight);
            GUI.Box(boxRect, $"Page {currentPage + 1}/{totalPages} ({uiState})", styleBox);

            // Display Items Dynamically
            GUILayout.BeginArea(new Rect(boxX + boxInset, boxY + 25, boxWidth - boxInset * 2, boxHeight));
            for (var i = 0; i < 10; i++) {
                var index = currentPage * ItemsPerPage + i;

                if (infos.TryGetValue(index, out var info)) {
                    GUILayout.Label($"{index}    {info.name}", style);
                } else {
                    GUILayout.Label($"{index}    (free)", style);
                }
            }

            GUILayout.EndArea();

            /*// Pagination Controls
            if (GUI.Button(new Rect(20, boxHeight + 10, 80, 30), "Prev") && currentPage > 0) {
                currentPage--;
            }

            if (GUI.Button(new Rect(120, boxHeight + 10, 80, 30), "Next") &&
                (currentPage + 1) * itemsPerPage < names.Length) {
                currentPage++;
            }*/
        }
    }

    private GUIStyle? style;
    private GUIStyle? styleBox;
}