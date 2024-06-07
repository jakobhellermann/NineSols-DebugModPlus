using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using Com.LuisPedroFonseca.ProCamera2D;
using DebugMod.Hitbox;
using HarmonyLib;
using InControl;
using InputExtension;
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

    private HitboxViewer hitboxViewer = new();

    private bool settingsOpen = true;

    public void LogInfo(string msg) {
        Logger.LogInfo(msg);
    }


    private class DebugActionToggle {
        public bool Value;
        public Action<bool> OnChange;
    }

    private Dictionary<string, DebugActionToggle> toggles = new();

    private void AddToggle(string actionName, Action<bool> onChange, bool defaultValue = false) {
        toggles.Add(actionName, new DebugActionToggle {
            Value = defaultValue,
            OnChange = onChange
        });
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

        toggles.Clear();
        AddToggle("FreeCam", OnFreecamChange);
        AddToggle("FastForward", OnFastForwardChange);
        AddToggle("Hitboxes", OnHitboxChange);
        AddToggle("Info Text", (_) => { });

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

        var toastTextTransform = toastText.GetComponent<RectTransform>();
        toastTextTransform.anchorMin = new Vector2(1, 0);
        toastTextTransform.anchorMax = new Vector2(1, 0);
        toastTextTransform.pivot = new Vector2(1f, 0f);
        toastTextTransform.anchoredPosition = new Vector2(-10, 10);
        toastTextTransform.sizeDelta = new Vector2(800f, 0f);


        ToastManager = new ToastManager();
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
            text += $"{state} {player.playerInput.fsm.State}\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel)
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})\n";

        text += $"{core.currentCutScene}";

        debugCanvasInfoText.text = text;
    }

    private void Update() {
        ToastManager.Update();

        if (actionSet != null) {
            if (actionSet.ToggleConsole.WasPressed && QuantumConsole.Instance)
                // CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
                QuantumConsole.Instance.Toggle();

            if (actionSet.ToggleSettings.WasPressed) {
                settingsOpen = !settingsOpen;
                
                UIManager.Instance.mapPanelController.ShouldShowDeathBag
                
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

        if (toggles["FreeCam"].Value) {
            var goFast = Input.GetKey(KeyCode.LeftShift);

            // var cam2d =
            // CameraManager.Instance.camera2D;


            var freecamSpeed = 200 * (goFast ? 3 : 1);

            CameraManager.Instance.dummyOffset = Vector2.zero;

            var input = new Vector2(
                Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical"));

            Player.i.SetPosition(Player.i.transform.position + (Vector3)(input * (Time.deltaTime * freecamSpeed)));

            CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);
        }

        if (toggles["Info Text"].Value)
            UpdateInfoText();
        else
            debugCanvasInfoText.text = "";
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();

        SceneManager.sceneLoaded -= OnSceneLoaded;

        Destroy(debugCanvas);
        actionSet.Destroy();

        hitboxViewer.Unload();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }

    private GUIStyle styleButton;
    private GUIStyle styleToggle;

    private PlayerInputStateType stateBefore = PlayerInputStateType.Cutscene;

    private void OnHitboxChange(bool visible) {
        if (visible)
            hitboxViewer.Load();
        else
            hitboxViewer.Unload();
    }

    private void OnFastForwardChange(bool ff) {
        ToastManager.Toast(RCGTime.GlobalSimulationSpeed.ToString());

        if (ff)
            RCGTime.GlobalSimulationSpeed = 2;
        else
            RCGTime.GlobalSimulationSpeed = 1;
    }

    private void OnFreecamChange(bool freecam) {
        var player = Player.i;
        var playerInput = Player.i.playerInput;

        if (freecam) {
            player.health.BecomeInvincible(this);
            stateBefore = playerInput.fsm.State;
            playerInput.fsm.ChangeState(PlayerInputStateType.Console);
            Player.i.DisableGravity();
        } else {
            player.health.RemoveInvincible(this);
            playerInput.fsm.ChangeState(stateBefore);
            Player.i.EnableGravity();
        }
    }


    private void OnGUI() {
        if (!settingsOpen) return;

        RCGInput.SetCursorVisible(true);

        const int padding = 20;
        if (styleButton == null) {
            styleButton = new GUIStyle(GUI.skin.box);
            styleButton.alignment = TextAnchor.MiddleRight;
            styleButton.padding = new RectOffset(padding, padding, padding, padding);
            styleButton.fontSize = 20;
        }

        if (styleToggle == null) {
            styleToggle = new GUIStyle(GUI.skin.toggle);
            // styleToggle.alignment = TextAnchor.MiddleLeft;
            // styleToggle.padding = new RectOffset(padding, padding, padding, padding);
            styleToggle.fontSize = 20;
        }

        GUILayout.BeginArea(new Rect(padding, padding, Screen.width - padding * 2, Screen.height - padding * 2));

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();


        GUILayout.BeginVertical();

        foreach (var (name, toggle) in toggles)
            if (GUILayout.Button($"{name}: {toggle.Value}", styleButton)) {
                toggle.Value = !toggle.Value;
                toggle.OnChange(toggle.Value);
                ToastManager.Toast($"change {name} to {toggle.Value}");
            }

        GUILayout.EndVertical();


        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }
}