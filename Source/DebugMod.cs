using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using DebugMod.Modules;
using DebugMod.Modules.Hitbox;
using HarmonyLib;
using NineSolsAPI;
using QFSW.QC;
using UnityEngine;

namespace DebugMod;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class DebugMod : BaseUnityPlugin {
    public static DebugMod Instance;

    private DebugUI debugUI;
    private QuantumConsoleModule quantumConsoleModule;

    private Harmony harmony;

    private InfotextModule infotextModule;
    public HitboxModule HitboxModule = new();
    public SavestateModule SavestateModule = new();
    public SpeedrunTimerModule SpeedrunTimerModule;
    public GhostModule GhostModule = new();


    private void Awake() {
        Instance = this;
        Log.Init(Logger);
        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} started loading...");

        try {
            harmony = Harmony.CreateAndPatchAll(typeof(DebugMod).Assembly);
            Log.Info($"Patched {harmony.GetPatchedMethods().Count()} methods...");
        } catch (Exception e) {
            Log.Error(e);
        }

        debugUI = gameObject.AddComponent<DebugUI>();
        quantumConsoleModule = new QuantumConsoleModule();
        infotextModule = new InfotextModule();
        SpeedrunTimerModule = new SpeedrunTimerModule();
        GhostModule = new GhostModule();

        SavestateModule.SavestateLoaded += (_, _) => SpeedrunTimerModule.OnSavestateLoaded();
        SavestateModule.SavestateCreated += (_, _) => SpeedrunTimerModule.OnSavestateCreated();


        KeybindManager.Add(this, ToggleConsole, KeyCode.LeftControl, KeyCode.Period);
        KeybindManager.Add(this, ToggleSettings, KeyCode.LeftControl, KeyCode.Comma);
        // KeybindManager.Add(this, () => GhostModule.ToggleRecording(), KeyCode.P);
        // KeybindManager.Add(this, () => GhostModule.Playback(GhostModule.CurrentRecording), KeyCode.O);

        var changeModeShortcut = Config.Bind("SpeedrunTimer", "Change Mode", new KeyboardShortcut());
        var setEndpointShortcut = Config.Bind("SpeedrunTimer", "Set Endpoint", new KeyboardShortcut());
        KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.SetEndpoint(), () => setEndpointShortcut.Value);

        var recordGhost = Config.Bind("SpeedrunTimer", "Record Ghost", false);
        // KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);


        debugUI.AddBindableMethods(Config, typeof(FreecamModule));
        debugUI.AddBindableMethods(Config, typeof(TimeModule));
        debugUI.AddBindableMethods(Config, typeof(InfotextModule));
        debugUI.AddBindableMethods(Config, typeof(HitboxModule));
        debugUI.AddBindableMethods(Config, typeof(SavestateModule));
        debugUI.AddBindableMethods(Config, typeof(CheatModule));

        RCGLifeCycle.DontDestroyForever(gameObject);

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }


    private void ToggleConsole() {
        if (!QuantumConsole.Instance) return;
        //CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
        QuantumConsole.Instance.Toggle();
    }

    private void ToggleSettings() {
        debugUI.settingsOpen = !debugUI.settingsOpen;
        if (Player.i is not null) {
            // if (settingsOpen) {
            // stateBefore = Player.i.playerInput.fsm.State;
            // Player.i.playerInput.fsm.ChangeState(PlayerInputStateType.Console);
            // } else
            // Player.i.playerInput.fsm.ChangeState(stateBefore);
        }
    }

    private void Update() {
        FreecamModule.Update();
        MapTeleportModule.Update();
        infotextModule.Update();
    }

    private void LateUpdate() {
        try {
            GhostModule.LateUpdate();
            SpeedrunTimerModule.LateUpdate();
        } catch (Exception e) {
            Log.Error(e);
        }
    }

    private void OnGUI() {
        SpeedrunTimerModule.OnGui();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        HitboxModule.Unload();
        SavestateModule.Unload();
        quantumConsoleModule.Unload();
        GhostModule.Unload();
        SpeedrunTimerModule.Destroy();

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}