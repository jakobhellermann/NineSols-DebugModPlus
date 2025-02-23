using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using NineSolsAPI;
using UnityEngine;

namespace DebugModPlus.Modules;

internal class Savestate {
    public required string MetaJson;
    public required byte[] Flags;

    public required string Scene;
    public Vector3 PlayerPosition;
    public Vector2 PlayerVelocity;
}

public class SavestateModule {
    public static bool IsLoadingSavestate = false;

    public event EventHandler? SavestateLoaded;
    public event EventHandler? SavestateCreated;

    private Dictionary<string, Savestate> savestates = new();

    public void Unload() {
        savestates.Clear();
    }

    #region Entrypoints

    [BindableMethod(Name = "Create Savestate")]
    private static void CreateSavestateMethod() {
        var module = DebugModPlus.Instance.SavestateModule;

        const string slot = "0";
        try {
            var sw = Stopwatch.StartNew();
            module.CreateSavestate(slot);
            Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e.Message);
            return;
        }

        ToastManager.Toast($"Savestate {slot} created");
    }

    [BindableMethod(Name = "Load Savestate")]
    private static void LoadSavestateMethod() {
        var module = DebugModPlus.Instance.SavestateModule;

        const string slot = "0";
        if (!module.savestates.TryGetValue(slot, out var savestate)) {
            ToastManager.Toast($"Savestate {slot} not found");
            return;
        }

        try {
            var sw = Stopwatch.StartNew();
            module.LoadSavestate(savestate);
            Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    [BindableMethod(Name = "Load Savestate\n(No reload)")]
    private static void LoadSavestateMethodNoReload() {
        var module = DebugModPlus.Instance.SavestateModule;

        const string slot = "0";
        if (!module.savestates.TryGetValue(slot, out var savestate)) {
            ToastManager.Toast($"Savestate '{slot}' not found");
            return;
        }

        try {
            var sw = Stopwatch.StartNew();
            module.LoadSavestate(savestate, false);
            Log.Info($"Loaded savestate {slot} in {sw.ElapsedMilliseconds}ms");
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    #endregion

    #region Create Savestate

    // TODO: Save Player Data, Reset Jades, and write to file
    //      HP, Direction, Qi, Ammo, Revival Jade
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
        var currentVelocity = player.Velocity;
        var saveSlotMetaData = gameCore.playerGameData.SaveMetaData();
        var metaJson = JsonUtility.ToJson(saveSlotMetaData);

        var savestate = new Savestate {
            MetaJson = metaJson,
            Flags = EncodeFlags(SaveManager.Instance.allFlags),
            Scene = gameCore.gameLevel.gameObject.scene.name,
            PlayerPosition = currentPos,
            PlayerVelocity = currentVelocity,
        };

        savestates[slot] = savestate;

        SavestateCreated?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Load Savestate

    // TODO: Implement loading from file
    private void LoadSavestate(Savestate savestate, bool reload = true) {
        IsLoadingSavestate = true;
        var saveManager = SaveManager.Instance;

        // var meta = JsonUtility.FromJson<SaveSlotMetaData>(savestate.MetaJson);


        // saveManager.allStatData.ClearStats();
        // saveManager.allFlags.AllFlagAwake(TestMode.Build);
        LoadFlags(savestate.Flags, SaveManager.Instance.allFlags);
        saveManager.allFlags.AllFlagInitStartAndEquip();

        // var teleportPointWithPath = GameFlagManager.Instance.GetTeleportPointWithPath(meta.lastTeleportPointPath);
        // ApplicationUIGroupManager.Instance.PopAll();

        // reload scene
        if (reload) {
            var currentPos = savestate.PlayerPosition;
            GameCore.Instance.ChangeSceneCompat(
                new SceneConnectionPoint.ChangeSceneData {
                    sceneName = savestate.Scene,
                    panData = { panType = SceneConnectionPoint.CameraPanType.NoPan, fromPosition = currentPos },
                    // changeSceneMode = SceneConnectionPoint.Cha   ngeSceneMode.Teleport
                    playerSpawnPosition = () => currentPos,
                    ChangedDoneEvent = () => {
                        Player.i.Velocity = savestate.PlayerVelocity;
                        OnSavestateLoaded();
                    },
                },
                false
            );
        }
        // no reload
        else {
            MapTeleportModule.TeleportTo(savestate.PlayerPosition, savestate.Scene, false);
            Player.i.Velocity = savestate.PlayerVelocity;

            OnSavestateLoaded();
        }

        IsLoadingSavestate = false;
    }

    private void OnSavestateLoaded() {
        // reset level cuz enemies often get killed and scene transition doesnt reset it
        GameCore.Instance.ResetLevel();
        SavestateLoaded?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Flag Load/Save

    private static MethodInfo? oldLoadFlagsMethodInfo = typeof(GameFlagManager).GetMethod("LoadFlags");

    private static MethodInfo? newLoadFlagsMethodInfo =
        typeof(GameFlagManager).GetMethod("LoadFlagsFromBinarySave");

    private void LoadFlags(byte[] flags, GameFlagCollection into) {
        const TestMode testMode = TestMode.Build;
        if (oldLoadFlagsMethodInfo != null) {
            oldLoadFlagsMethodInfo.Invoke(null,
                new object[] { Encoding.UTF8.GetString(flags), into, testMode });
        } else {
            if (newLoadFlagsMethodInfo == null) {
                Log.Error("LoadFlagsFromBinarySave doesn't exist");
                return;
            }

            newLoadFlagsMethodInfo.Invoke(null, new object[] { flags, into, testMode });
        }
    }

    private static MethodInfo? flagsToBinary =
        typeof(GameFlagManager).GetMethod("FlagsToBinary");

    private byte[] EncodeFlags(GameFlagCollection flags) {
        return flagsToBinary != null
            ? (byte[])flagsToBinary.Invoke(null, new object[] { flags })
            : Encoding.UTF8.GetBytes(GameFlagManager.FlagsToJson(flags));
    }

    #endregion
}