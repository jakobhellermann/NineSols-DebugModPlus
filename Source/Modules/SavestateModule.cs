using System;
using System.Collections.Generic;
using NineSolsAPI;
using UnityEngine;

namespace DebugMod.Modules;

internal class Savestate {
    public string MetaJson;
    public string FlagsJson;

    public string Scene;
    public Vector3 PlayerPosition;
    public Vector2 PlayerVelocity;
}

public class SavestateModule {
    public static bool IsLoadingSavestate = false;

    public event EventHandler SavestateLoaded;
    public event EventHandler SavestateCreated;

    // TODO unload
    private Dictionary<string, Savestate> savestates = [];


    [BindableMethod(Name = "Create Savestate")]
    private static void CreateSavestateMethod() {
        var module = DebugMod.Instance.SavestateModule;

        const string slot = "0";
        module.CreateSavestate(slot);
        ToastManager.Toast($"Savestate '{slot}' created");
    }

    [BindableMethod(Name = "Load Savestate")]
    private static void LoadSavestateMethod() {
        var module = DebugMod.Instance.SavestateModule;

        const string slot = "0";
        if (!module.savestates.TryGetValue(slot, out var savestate)) {
            ToastManager.Toast($"Savestate '{slot}' not found");
            return;
        }

        try {
            module.LoadSavestate(savestate);
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    [BindableMethod(Name = "Load Savestate\n(No reload)")]
    private static void LoadSavestateMethodNoReload() {
        var module = DebugMod.Instance.SavestateModule;

        const string slot = "0";
        if (!module.savestates.TryGetValue(slot, out var savestate)) {
            ToastManager.Toast($"Savestate '{slot}' not found");
            return;
        }

        try {
            module.LoadSavestateNoReload(savestate);
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }


    private void CreateSavestate(string slot) {
        var saveManager = SaveManager.Instance;
        var gameCore = GameCore.Instance;


        var player = Player.i;
        var currentPos = player.transform.position;
        var currentVelocity = player.Velocity;
        var flagsJson = GameFlagManager.FlagsToJson(saveManager.allFlags);
        var saveSlotMetaData = gameCore.playerGameData.SaveMetaData();
        var metaJson = JsonUtility.ToJson(saveSlotMetaData);


        var savestate = new Savestate {
            MetaJson = metaJson,
            FlagsJson = flagsJson,
            Scene = gameCore.gameLevel.gameObject.scene.name,
            PlayerPosition = currentPos,
            PlayerVelocity = currentVelocity,
        };

        savestates[slot] = savestate;

        SavestateCreated?.Invoke(this, EventArgs.Empty);
    }


    private void LoadSavestate(Savestate savestate) {
        IsLoadingSavestate = true;

        var saveManager = SaveManager.Instance;

        var meta = JsonUtility.FromJson<SaveSlotMetaData>(savestate.MetaJson);

        // saveManager.allStatData.ClearStats();
        // saveManager.allFlags.AllFlagAwake(TestMode.Build);
        GameFlagManager.LoadFlags(
            savestate.FlagsJson,
            saveManager.allFlags,
            TestMode.Build
        );
        saveManager.allFlags.AllFlagInitStartAndEquip();

        // var teleportPointWithPath = GameFlagManager.Instance.GetTeleportPointWithPath(meta.lastTeleportPointPath);
        //ApplicationUIGroupManager.Instance.PopAll();

        // reload scene
        var currentPos = savestate.PlayerPosition;
        GameCore.Instance.ChangeScene(
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

        ToastManager.Toast("Savestate loaded");
        IsLoadingSavestate = false;
    }


    private void LoadSavestateNoReload(Savestate savestate) {
        IsLoadingSavestate = true;

        var saveManager = SaveManager.Instance;
        // var meta = JsonUtility.FromJson<SaveSlotMetaData>(savestate.MetaJson);

        GameFlagManager.LoadFlags(
            savestate.FlagsJson,
            saveManager.allFlags,
            TestMode.Build
        );
        saveManager.allFlags.AllFlagInitStartAndEquip();
        GameCore.Instance.ResetLevel();

        MapTeleportModule.TeleportTo(savestate.PlayerPosition, savestate.Scene, false);
        Player.i.Velocity = savestate.PlayerVelocity;

        OnSavestateLoaded();

        ToastManager.Toast("Savestate loaded");
        IsLoadingSavestate = false;
    }

    private void OnSavestateLoaded() {
        SavestateLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void Unload() {
        savestates = [];
    }
}