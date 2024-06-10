using System;
using System.Linq;
using BepInEx;
using DebugMod.Modules;
using DebugMod.Modules.Hitbox;
using HarmonyLib;
using NineSolsAPI;
using QFSW.QC;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        KeybindManager.Add(ToggleConsole, KeyCode.LeftControl, KeyCode.Period);
        KeybindManager.Add(ToggleSettings, KeyCode.LeftControl, KeyCode.Comma);

        debugUI.AddBindableMethods(typeof(FreecamModule));
        debugUI.AddBindableMethods(typeof(TimeModule));
        debugUI.AddBindableMethods(typeof(InfotextModule));
        debugUI.AddBindableMethods(typeof(HitboxModule));
        debugUI.AddBindableMethods(typeof(SavestateModule));

        RCGLifeCycle.DontDestroyForever(gameObject);

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void Start() {
        Invoke(nameof(AfterStart), 0);
    }

    private void AfterStart() {
        if (SceneManager.GetActiveScene().name == "Logo") SceneManager.LoadScene("TitleScreenMenu");
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
        infotextModule.Update();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        HitboxModule.Unload();
        SavestateModule.Unload();
        quantumConsoleModule.Unload();
        // actionSet.Destroy();

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}