#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using DebugMod.Modules;
using DebugMod.Modules.Hitbox;
using HarmonyLib;
using MonsterLove.StateMachine;
using NineSolsAPI;
using UnityEngine;
using UnityEngine.XR;

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

    public FsmInspectorModule FsmInspectorModule;
    public GhostModule GhostModule = new();

    private ConfigEntry<KeyboardShortcut> configShortcutFSMPickerModifier;


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
        FsmInspectorModule = new FsmInspectorModule();
        GhostModule = new GhostModule();

        SavestateModule.SavestateLoaded += (_, _) => SpeedrunTimerModule.OnSavestateLoaded();
        SavestateModule.SavestateCreated += (_, _) => SpeedrunTimerModule.OnSavestateCreated();

        KeybindManager.Add(this, quantumConsoleModule.ToggleConsole, KeyCode.LeftControl, KeyCode.Period);
        KeybindManager.Add(this, ToggleSettings, KeyCode.LeftControl, KeyCode.Comma);
        // KeybindManager.Add(this, () => GhostModule.ToggleRecording(), KeyCode.P);
        // KeybindManager.Add(this, () => GhostModule.Playback(GhostModule.CurrentRecording), KeyCode.O);

        var changeModeShortcut = Config.Bind("SpeedrunTimer", "Change Mode", new KeyboardShortcut());
        var resetTimerShortcut = Config.Bind("SpeedrunTimer", "Reset Timer", new KeyboardShortcut());
        var pauseTimerShortcut = Config.Bind("SpeedrunTimer", "Pause Timer", new KeyboardShortcut());
        var setStartpointShortcut = Config.Bind("SpeedrunTimer", "Set Startpoint", new KeyboardShortcut());
        var setEndpointShortcut = Config.Bind("SpeedrunTimer", "Set Endpoint", new KeyboardShortcut());
        KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.ResetTimer(), () => resetTimerShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.PauseTimer(), () => pauseTimerShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.SetStartpoint(), () => setStartpointShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.SetEndpoint(), () => setEndpointShortcut.Value);

        // var recordGhost = Config.Bind("SpeedrunTimer", "Record Ghost", false);
        // KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);


        debugUI.AddBindableMethods(Config, typeof(FreecamModule));
        debugUI.AddBindableMethods(Config, typeof(TimeModule));
        debugUI.AddBindableMethods(Config, typeof(InfotextModule));
        debugUI.AddBindableMethods(Config, typeof(HitboxModule));
        debugUI.AddBindableMethods(Config, typeof(SavestateModule));
        debugUI.AddBindableMethods(Config, typeof(CheatModule));
        // debugUI.AddBindableMethods(Config, typeof(FlagLoggerModule));
        FlagLoggerModule.Awake();

        configShortcutFSMPickerModifier = Config.Bind("Shortcuts", "FSM Picker Modifier",
            new KeyboardShortcut(KeyCode.LeftControl),
            new ConfigDescription(
                "When this key is pressed and you click on a sprite, it will try to open the FSM inspector for that object"));

        RCGLifeCycle.DontDestroyForever(gameObject);

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
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

        if (configShortcutFSMPickerModifier.Value.IsPressed()) {
            Cursor.visible = true;

            if (Input.GetMouseButtonDown(0)) {
                FsmInspectorModule.Objects.Clear();

                try {
                    var mainCamera = CameraManager.Instance.cameraCore.theRealSceneCamera;
                    var worldPosition =
                        mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                            -mainCamera.transform.position.z));
                    worldPosition.z = 0; // Set z to 0 to match the 2D plane
                    var visible = PickVisible(worldPosition);

                    var stateMachine = visible.Select(sprite =>
                            sprite.GetComponentInParent<StateMachineOwner>()?.gameObject ??
                            sprite.GetComponentInParent<FSMStateMachineRunner>()?.gameObject)
                        .Where(x => x)
                        .Distinct()
                        .FirstOrDefault();
                    if (stateMachine)
                        FsmInspectorModule.Objects.Add(stateMachine!);
                    else
                        ToastManager.Toast($"No state machine found at cursor");
                } catch (Exception e) {
                    ToastManager.Toast(e);
                }
            }
        }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private List<GameObject> PickVisible(Vector3 worldPosition) {
        return FindObjectsOfType<SpriteRenderer>().Select(renderer => (renderer.gameObject, renderer.bounds))
            .Concat(FindObjectsOfType<ParticleSystemRenderer>()
                .Select(renderer => (renderer.gameObject, renderer.bounds)))
            .Where(t => t.bounds.Contains(worldPosition))
            .Where(t => {
                var goName = t.gameObject.gameObject.name.ToLower();
                var parentName = t.gameObject.gameObject.transform.parent?.name ?? "";
                return !goName.Contains("light") && !goName.Contains("fade") &&
                       !goName.Contains("glow") && !goName.Contains("attack") &&
                       !parentName.Contains("Vibe") && !parentName.Contains("Skin");
            })
            .Select(x => x.gameObject)
            .ToList();
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
        FsmInspectorModule.OnGui();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        HitboxModule.Unload();
        SavestateModule.Unload();
        GhostModule.Unload();
        SpeedrunTimerModule.Destroy();
        infotextModule.Destroy();

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}