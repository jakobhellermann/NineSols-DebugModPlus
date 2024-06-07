using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using BepInEx;
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

    private Harmony harmony;
    private GameObject debugCanvas;
    private TMP_Text debugCanvasInfoText;
    private DebugModActionSet actionSet;

    public void LogInfo(string msg) {
        Logger.LogInfo(msg);
    }

    private class DebugModActionSet : PlayerActionSet {
        public PlayerAction ToggleConsole;

        public void Initialize() {
            ToggleConsole = CreatePlayerAction("Test");
            ToggleConsole.AddDefaultBinding(Key.Control, Key.Period);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
        Logger.LogInfo($"Scene loaded: {scene.name}");
        if (scene.name == "TitleScreenMenu") {
            actionSet = new DebugModActionSet();
            actionSet.Initialize();
        }
    }

    private void Awake() {
        Instance = this;

        harmony = Harmony.CreateAndPatchAll(typeof(Patches));
        SceneManager.sceneLoaded += OnSceneLoaded;

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} started loading...");
        Logger.LogInfo($"Patched {harmony.GetPatchedMethods().Count()} started loading...");

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
        toastText.text = "start";

        var toastTextTransform = toastText.GetComponent<RectTransform>();
        toastTextTransform.anchorMin = new Vector2(1, 0);
        toastTextTransform.anchorMax = new Vector2(1, 0);
        toastTextTransform.pivot = new Vector2(1f, 0f);
        toastTextTransform.anchoredPosition = new Vector2(-10, 10);
        toastTextTransform.sizeDelta = new Vector2(800f, 0f);


        ToastManager = new ToastManager();
        ToastManager.Initialize(toastText);

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        RCGLifeCycle.DontDestroyForever(gameObject);
        RCGLifeCycle.DontDestroyForever(debugCanvas);
    }

    private void Start() {
        Invoke(nameof(AfterStart), 0);
    }

    private void AfterStart() {
        SceneManager.LoadScene("TitleScreenMenu");
    }


    private void UpdateInfoText() {
        var text = "";

        if (!SingletonBehaviour<GameCore>.IsAvailable()) {
            text += "not yet available\n";
            debugCanvasInfoText.text = text;
            return;
        }

        var core = GameCore.Instance;

        var coreState = typeof(GameCore.GameCoreState).GetEnumName(core.currentCoreState);
        text += $"{coreState}\n";

        var player = core.player;
        if (player) {
            text += $"Pos: {player.transform.position}\n";
            text += $"Speed: {player.Velocity}\n";
            text += $"HP: {player.health.CurrentHealthValue} (+{player.health.CurrentInternalInjury})\n";
            var state = typeof(PlayerStateType).GetEnumName(player.fsm.State);
            text += $"{state}\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel)
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})\n";

        text += $"{core.currentCutScene}";

        debugCanvasInfoText.text = text;
    }

    private void Update() {
        ToastManager.Update();

        if (actionSet != null)
            if (actionSet.ToggleConsole.WasPressed && QuantumConsole.Instance)
                // CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
                QuantumConsole.Instance.Toggle();

        UpdateInfoText();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Destroy(debugCanvas);
        actionSet.Destroy();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }


    private void CallPrivateMethod(Type type, string methodName, object obj) {
        var method = type.GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(obj, []);
    }

    private T GetPrivateField<T>(Type type, string name, object obj) {
        var field = type.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (T)field.GetValue(obj);
    }

    private T GetPrivateFieldStatic<T>(Type type, string name) {
        var field = type.GetField(name,
            BindingFlags.Static | BindingFlags.NonPublic);
        return (T)field.GetValue(null);
    }
}