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
    ConfigEntry<SavestateLoadMode> loadMode,
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

    private const string QuicksaveSlot = "quicksave";

    [BindableMethod(Name = "Create Quicksave")]
    private static void CreateSavestateMethod() {
        DebugModPlus.Instance.SavestateModule.CreateSavestate(QuicksaveSlot);
    }


    [BindableMethod(Name = "Load Quicksave")]
    private static void LoadSavestateMethodNoReload() {
        _ = DebugModPlus.Instance.SavestateModule.LoadSavestateAt(QuicksaveSlot);
    }

    #endregion

    public bool CreateSavestate(string name, int? slot = null, string? layer = null, SavestateFilter? filter = null) {
        try {
            var sw = Stopwatch.StartNew();
            var savestate = SavestateLogic.Create(filter ?? currentFilter.Value);
            savestates.Save(name, savestate, slot, layer);
            Log.Info($"Created savestate {name} in {sw.ElapsedMilliseconds}ms");

            SavestateCreated?.Invoke(this, EventArgs.Empty);
            ToastManager.Toast($"Savestate {name} created");
            return true;
        } catch (Exception e) {
            ToastManager.Toast(e.Message);
            return false;
        }
    }

    public async Task<bool> LoadSavestateAt(string fullName) {
        var sw = Stopwatch.StartNew();
        if (!savestates.TryGetValue(fullName, out var savestate)) {
            ToastManager.Toast($"Savestate '{fullName}' not found");
            return false;
        }

        Log.Debug($"- Reading state from disk {sw.ElapsedMilliseconds}ms");
        await LoadSavestate(savestate);
        Log.Info($"Loading savestate {fullName} in {sw.ElapsedMilliseconds}ms");
        return true;
    }

    // Enforces single loading and enters debug save if necessary
    private async Task<bool> LoadSavestate(Savestate savestate) {
        if (IsLoadingSavestate) {
            Log.Error("Attempted to load savestate while loading savestate");
            return false;
        }

        try {
            IsLoadingSavestate = true;

            if (!GameCore.IsAvailable()) {
                DebugSave.LoadDebugSave();
                var tp = GameFlagManager.Instance.GetTeleportPointWithPath(savestate.LastTeleportId);
                ApplicationCore.Instance.lastSaveTeleportPoint = tp;
                await ApplicationCore.Instance.ChangeSceneCompat(tp.sceneName);
                // TODO: figure out what to wait for
                await UniTask.DelayFrame(10);
            }

            await SavestateLogic.Load(savestate, loadMode.Value);
            SavestateLoaded?.Invoke(this, EventArgs.Empty);
            return true;
        } catch (Exception e) {
            ToastManager.Toast(e);
            return false;
        } finally {
            IsLoadingSavestate = false;
        }
    }

    #region UI

    private void UiSaveToSlot(int slot) {
        var scene = GameCore.Instance.gameLevel.SceneName;
        var defaultName = $"{scene} {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
        CreateSavestate(defaultName, slot, savestateLayer);
    }

    private async void UiLoadFromSlot(int slot) {
        var bySlot = savestates.List(slot, savestateLayer).ToList();
        switch (bySlot.Count) {
            case 0:
                ToastManager.Toast($"Savestate '{slot}' not found");
                return;
            case > 1:
                ToastManager.Toast($"Multiple savestates found at slot {slot}, picking {bySlot[0].FullName}");
                break;
        }

        if (!SavestateStore.TryGetValue(bySlot[0], out var savestate)) {
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


    private string? savestateLayer = null;

    private SavestateUIState uiState = SavestateUIState.Off;

    private SavestateUIState UiState {
        get => uiState;
        set {
            uiState = value;
            if (uiState == SavestateUIState.Off) {
                // PlayerInputBinder.Instance.RevokeAllMyVote(DebugModPlus.Instance);
                RCGTime.GlobalSimulationSpeed = 1;
            } else {
                // PlayerInputBinder.Instance.VoteForState(PlayerInputStateType.Console + 1, DebugModPlus.Instance);
                RCGTime.GlobalSimulationSpeed = 0;
            }
        }
    }

    private Dictionary<int, SavestateInfo> infos = [];

    private void LoadInfos() {
        infos.Clear();
        foreach (var info in savestates.List(layer: savestateLayer)) {
            if (info.index is not { } index) continue;

            infos.TryAdd(index, info);
        }
    }

    private static string? SavestateLayerFromModifiers() =>
        Input.GetKey(KeyCode.LeftControl) | Input.GetKey(KeyCode.LeftControl)
            ? "Secondary"
            : null;

    private void UpdateLayer() {
        var newLayer = SavestateLayerFromModifiers();
        if (newLayer != savestateLayer) {
            currentPage = 0;
        }

        savestateLayer = newLayer;
    }

    public void Update() {
        try {
            if (KeybindManager.CheckShortcutOnly(openLoad.Value)) {
                UiState = UiState == SavestateUIState.Load ? SavestateUIState.Off : SavestateUIState.Load;
                UpdateLayer();
            } else if (KeybindManager.CheckShortcutOnly(openSave.Value)) {
                UiState = UiState == SavestateUIState.Save ? SavestateUIState.Off : SavestateUIState.Save;
                UpdateLayer();
            } else if (KeybindManager.CheckShortcutOnly(openDelete.Value)) {
                UiState = UiState == SavestateUIState.Delete ? SavestateUIState.Off : SavestateUIState.Delete;
                UpdateLayer();
            } else if (KeybindManager.CheckShortcutOnly(tabNext.Value)) {
                currentPage++;
            } else if (KeybindManager.CheckShortcutOnly(tabPrev.Value)) {
                currentPage = Math.Max(currentPage - 1, 0);
            }

            if (UiState != SavestateUIState.Off) {
                LoadInfos();
            }

            if (UiState != SavestateUIState.Off) {
                if (Input.GetKeyDown(KeyCode.Escape)) UiState = SavestateUIState.Off;

                for (var i = 0; i < 10; i++) {
                    if (!Input.GetKeyDown(KeyCode.Alpha0 + i)
                        && !Input.GetKeyDown(KeyCode.Keypad0 + i)) continue;

                    var saveIndex = currentPage * ItemsPerPage + i;

                    if (UiState == SavestateUIState.Save) {
                        UiSaveToSlot(saveIndex);
                        UiState = SavestateUIState.Off;
                    } else if (UiState == SavestateUIState.Load) {
                        UiLoadFromSlot(saveIndex);
                        UiState = SavestateUIState.Off;
                    } else if (UiState == SavestateUIState.Delete) {
                        savestates.Delete(saveIndex, savestateLayer);
                        UiState = SavestateUIState.Off;
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

        if (UiState != SavestateUIState.Off) {
            var maxSlotIndex = infos.Count > 0 ? infos.Max(kv => kv.Key) : 0;
            var totalPages = Math.Max(Mathf.CeilToInt((float)maxSlotIndex / ItemsPerPage), currentPage + 1);

            const int itemHeight = 27;
            // var visibleItems = Mathf.Min(ItemsPerPage, infos.Count);
            var visibleItems = ItemsPerPage;
            var boxHeight = (visibleItems + 2) * itemHeight;

            const int boxWidth = 450;
            const int boxInset = 10;
            var boxX = Screen.width / 2 - boxWidth / 2;
            const int boxY = 50;

            var boxRect = new Rect(boxX, boxY, boxWidth, boxHeight);
            var layer = savestateLayer != null ? $" {savestateLayer}" : "";
            GUI.Box(boxRect, $"Page {currentPage + 1}/{totalPages}{layer} ({UiState})", styleBox);

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

    #endregion
}