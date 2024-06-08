using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using DebugMod.Source;
using DebugMod.Source.Modules;
using DebugMod.Source.Modules.Hitbox;
using HarmonyLib;
using InControl;
using QFSW.QC;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod;

[HarmonyPatch]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class Patches {
    [HarmonyPatch(typeof(QuantumConsoleProcessor), "LoadCommandsFromType")]
    [HarmonyFinalizer]
    private static Exception LoadCommandsFromType(Type type, Exception __exception) => null;

    [HarmonyPatch(typeof(QuantumConsole), "IsSupportedState")]
    [HarmonyPrefix]
    private static bool IsSupportedState(ref bool __result) {
        __result = true;
        return false;
    }
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public static Plugin Instance;

    public ToastManager ToastManager;
    private DebugUI debugUI;

    private Harmony harmony;
    private GameObject debugCanvas;
    private TMP_Text debugCanvasInfoText;
    private DebugModActionSet actionSet;

    private InfotextModule infotextModule;
    public HitboxModule HitboxModule = new();

    public void LogInfo(string msg) {
        Logger.LogInfo(msg);
    }


    private class DebugModActionSet : PlayerActionSet {
        public PlayerAction ToggleConsole;
        public PlayerAction ToggleSettings;

        public void Initialize() {
            ToggleConsole = CreatePlayerAction("Toggle Console");
            ToggleSettings = CreatePlayerAction("Toggle Settings");

            ToggleConsole.AddDefaultBinding(Key.Control, Key.Period);
            ToggleSettings.AddDefaultBinding(Key.Control, Key.Comma);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
        Logger.LogInfo($"Scene loaded: {scene.name}");
        LoadActionSet();
    }

    private void LoadActionSet() {
        if (actionSet == null && InputManager.IsSetup) {
            actionSet = new DebugModActionSet();
            actionSet.Initialize();
        }
    }

    private void Awake() {
        Instance = this;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} started loading...");

        harmony = Harmony.CreateAndPatchAll(typeof(Patches));
        Logger.LogInfo($"Patched {harmony.GetPatchedMethods().Count()} methods...");

        SceneManager.sceneLoaded += OnSceneLoaded;
        ToastManager = new ToastManager();
        debugUI = gameObject.AddComponent<DebugUI>();

        try {
            debugUI.AddBindableMethods(typeof(FreecamModule));
            debugUI.AddBindableMethods(typeof(TimeModule));
            debugUI.AddBindableMethods(typeof(InfotextModule));
            debugUI.AddBindableMethods(typeof(HitboxModule));
            debugUI.AddBindableMethods(typeof(SavestateModule));
        } catch (Exception e) {
            Logger.LogError(e);
            ToastManager.Toast(e);
            throw;
        }

        debugCanvas = new GameObject("DebugCanvas");
        var canvas = debugCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var debugText = new GameObject();
        debugText.transform.SetParent(debugCanvas.transform);
        debugCanvasInfoText = debugText.AddComponent<TextMeshProUGUI>();
        debugCanvasInfoText.alignment = TextAlignmentOptions.TopLeft;
        debugCanvasInfoText.fontSize = 20;
        debugCanvasInfoText.color = Color.white;

        var debugTextTransform = debugCanvasInfoText.GetComponent<RectTransform>();
        debugTextTransform.anchorMin = new Vector2(0, 1);
        debugTextTransform.anchorMax = new Vector2(0, 1);
        debugTextTransform.pivot = new Vector2(0f, 1f);
        debugTextTransform.anchoredPosition = new Vector2(10, -10);
        debugTextTransform.sizeDelta = new Vector2(800f, 0f);

        var toastTextObj = new GameObject();
        toastTextObj.transform.SetParent(debugCanvas.transform);
        var toastText = toastTextObj.AddComponent<TextMeshProUGUI>();
        toastText.alignment = TextAlignmentOptions.BottomRight;
        toastText.fontSize = 20;
        toastText.color = Color.white;

        var toastTextTransform = toastText.GetComponent<RectTransform>();
        toastTextTransform.anchorMin = new Vector2(1, 0);
        toastTextTransform.anchorMax = new Vector2(1, 0);
        toastTextTransform.pivot = new Vector2(1f, 0f);
        toastTextTransform.anchoredPosition = new Vector2(-10, 10);
        toastTextTransform.sizeDelta = new Vector2(800f, 0f);

        infotextModule = new InfotextModule(debugCanvasInfoText);

        ToastManager.Initialize(toastText);

        LoadActionSet();


        RCGLifeCycle.DontDestroyForever(gameObject);
        RCGLifeCycle.DontDestroyForever(debugCanvas);


        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void Start() {
        Invoke(nameof(AfterStart), 0);
    }

    private void AfterStart() {
        if (SceneManager.GetActiveScene().name == "Logo") SceneManager.LoadScene("TitleScreenMenu");
    }


    private void Update() {
        ToastManager.Update();

        FreecamModule.Update();
        infotextModule.Update();

        if (actionSet != null) {
            if (actionSet.ToggleConsole.WasPressed && QuantumConsole.Instance)
                // CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
                QuantumConsole.Instance.Toggle();

            if (actionSet.ToggleSettings.WasPressed) {
                debugUI.settingsOpen = !debugUI.settingsOpen;

                if (Player.i is not null) {
                    // if (settingsOpen) {
                    // stateBefore = Player.i.playerInput.fsm.State;
                    // Player.i.playerInput.fsm.ChangeState(PlayerInputStateType.Console);
                    // } else
                    // Player.i.playerInput.fsm.ChangeState(stateBefore);
                }
            }
        }
        // CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Destroy(debugCanvas);
        actionSet.Destroy();

        HitboxModule.Unload();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}