using NineSolsAPI;
using TMPro;
using UnityEngine;

namespace DebugMod.Modules;

public class InfotextModule {
    private static bool infotextActive;
    private TMP_Text debugCanvasInfoText;

    public InfotextModule() {
        var debugText = new GameObject();
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
}