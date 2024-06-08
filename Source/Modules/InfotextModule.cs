using TMPro;

namespace DebugMod.Source.Modules;

public class InfotextModule(TMP_Text debugCanvasInfoText) {
    private static bool infotextActive;

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