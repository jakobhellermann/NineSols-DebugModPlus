using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NineSolsAPI;
using TMPro;
using UnityEngine;

namespace DebugMod.Modules;

public class InfotextModule {
    private static bool infotextActive = false;
    private TMP_Text debugCanvasInfoText;

    private AccessTools.FieldRef<Player, float> groundJumpReferenceY =
        AccessTools.FieldRefAccess<Player, float>("GroundJumpRefrenceY");

    public InfotextModule() {
        var debugText = new GameObject("Info Text");
        debugText.transform.SetParent(NineSolsAPICore.FullscreenCanvas.gameObject.transform);
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
    }

    [BindableMethod(Name = "Toggle Infotext")]
    private static void ToggleFreecam() {
        infotextActive = !infotextActive;
    }

    public void Update() {
        if (infotextActive)
            UpdateInfoText();
        else
            debugCanvasInfoText.text = "";
    }

    private void UpdateInfoText() {
        var text = "";

        if (!SingletonBehaviour<GameCore>.IsAvailable()) {
            text += "not yet available\n";
            debugCanvasInfoText.text = text;
            return;
        }

        var core = GameCore.Instance;

        if (core.currentCoreState != GameCore.GameCoreState.Playing) {
            var coreState = typeof(GameCore.GameCoreState).GetEnumName(core.currentCoreState);
            text += $"{coreState}\n";
        }

        var player = core.player;
        if (player) {
            text += $"Pos: {(Vector2)player.transform.position}\n";
            text += $"Speed: {player.FinalVelocity}\n";
            text += $"HP: {player.health.CurrentHealthValue} (+{player.health.CurrentInternalInjury})\n";
            var state = typeof(PlayerStateType).GetEnumName(player.fsm.State);
            var inputState = player.playerInput.fsm.State;
            text += $"{state} {(inputState == PlayerInputStateType.Action ? "" : inputState.ToString())}\n";

            if (player.jumpState != Player.PlayerJumpState.None) {
                var varJumpTimer = player.currentVarJumpTimer;
                text +=
                    $"JumpState {player.jumpState} {(varJumpTimer > 0 ? varJumpTimer.ToString("0.00") : "")} {player.IsAirJumping}\n";
            } else text += "\n";

            List<(bool, string)> flags = [
                (player.isOnWall, "isOnWall"),
                (player.isOnLedge, "isOnLedge"),
                (player.isOnRope, "isOnRope"),
                (player.kicked, "kicked"),
            ];

            var flagsStr = flags.Where(x => x.Item1).Join(x => x.Item2, " ");

            text += $"{flagsStr}\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel)
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})\n";

        if (core.currentCutScene is not null) text += $"{core.currentCutScene}";

        if (player.interactableFinder.CurrentInteractableArea is { } current) {
            text += "Interaction:\n";
            foreach (var interaction in current.ValidInteractions)
                text += $"{interaction}";
        }

        debugCanvasInfoText.text = text;
    }

    public void Destroy() {
        Object.Destroy(debugCanvasInfoText.gameObject);
    }
}