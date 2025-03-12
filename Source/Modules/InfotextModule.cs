using System;
using TAS;
using UnityEngine;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using BepInEx.Configuration;

namespace DebugModPlus.Modules;

public class InfotextModule {
    [Flags]
    public enum InfotextFilter {
        None = 0,
        GameInfo = 1 << 1,
        BasicPlayerInfo = 1 << 2,
        AdvancedPlayerInfo = 1 << 3,
        RespawnInfo = 1 << 4,
        DamageInfo = 1 << 5,
        EnemyInfo = 1 << 6,
        InteractableInfo = 1 << 7,
        DebugInfo = 1 << 8,

        All = GameInfo | BasicPlayerInfo | AdvancedPlayerInfo | RespawnInfo | DamageInfo | EnemyInfo | InteractableInfo,
    }

    private static bool infotextActive = false;
    private static ConfigEntry<InfotextFilter> filter;
    private string debugCanvasInfoText = "";

    private AccessTools.FieldRef<Player, float> groundJumpReferenceY =
        AccessTools.FieldRefAccess<Player, float>("GroundJumpRefrenceY");

    public InfotextModule(ConfigEntry<InfotextFilter> currentFilter) {
        filter = currentFilter;
    }

    [BindableMethod(Name = "Toggle Infotext")]
    private static void ToggleInfoText() {
        infotextActive = !infotextActive;
    }

    //TODO: Make info text not update each frame when in frame by frame
    //      However it cant just be a simple timescale check cuz it needs to work with bow !
    public void Update() {
        var text = "";
        if (infotextActive) {
            if (!SingletonBehaviour<GameCore>.IsAvailable()) {
                text += "\nMainMenu";
                text += PlayerInputBinder.Instance.currentStateType.ToString();
            } else if (RCGTime.timeScale > 0 || UIManager.Instance.PausePanelUI.isActiveAndEnabled) {
                text = UpdateInfoText();
            } else return;
        }

        debugCanvasInfoText = text;
    }


    private string UpdateInfoText() {
        /* [Debug v0.0.0]
         * -------------------------------
         * A11_S0_Boss_YiGung
         * Game State: Playing
         * Cutscene:   asdlkfjasdflk
         * -------------------------------
         * HP:         80/120 (10)
         * Int DMG:    30 (10)
         *
         * Position:  (1232.32, 12424.12)
         * Speed: (120, 0) Avg: (120, 40)
         * -------------------------------
         * Spawn Pos: (1200, 1400)
         * Safe Pos: (1200, 1400)
         * Scene Pos: (1230, 0)
         * -------------------------------
         * Player State:  Normal
         * Player Rope:   asdlkfj, 12312.123
         * Player Ledge:  True
         * Player Wall:   True
         * Player Kicked: True
         *
         * Jump State: Jumping
         * Jump Timer: 1.24s
         * -------------------------------
         * Tao Fruits: 2
         * Attack: 20 | Foo: 32
         * -------------------------------
         * Enemy HP: 400/1200
         * Total HP DMG:  400
         * Total INT DMG: 20
         * -------------------------------
         * Interactables:
         *    Door
         *    Chest
         *
         */
        var text = "";
        try {
            text = "[DEBUG+] (v" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + ")";
            const string spacer = "\n----------------------------------------------";

            var infoData = new GameInfo();
            if (infoData.ErrorText is not null) text += "\n" + infoData.ErrorText;
            text += spacer;

            if (filter.Value.HasFlag(InfotextFilter.GameInfo)) {
                text += "\n" + infoData.GameLevel;
                text += "\n" + "Game State: " + infoData.CoreState;
                text += "\n" + "Cutscene: " + infoData.Cutscene;
                text += spacer;
            }

            if (filter.Value.HasFlag(InfotextFilter.BasicPlayerInfo)) {
                text += "\n" + "HP: " + infoData.PlayerHp + "/" + infoData.PlayerMaxHp + " (" + infoData.PlayerLostHp +
                        ")";
                text += "\n" + "Int Dmg: " + infoData.PlayerIntDmg + " (" + infoData.PlayerIntNewDmg + ")";
                text += "\n";
                text += "\n" + "Position: " + infoData.PlayerPos;
                text += "\n" + "Speed: " + infoData.PlayerSpeed;
                text += "\n" + "Avg Speed: " + infoData.PlayerAvgSpeed;
                text += spacer;
            }

            if (filter.Value.HasFlag(InfotextFilter.RespawnInfo)) {
                text += "\n" + "Spawn Pos: " + infoData.PlayerRespawn;
                text += "\n" + "Safe Pos: " + infoData.PlayerSafePos;
                text += "\n" + "Scene Pos: " + infoData.SceneRespawn;
                text += spacer;
            }

            if (filter.Value.HasFlag(InfotextFilter.AdvancedPlayerInfo)) {
                text += "\n" + "Player State: " + infoData.playerState;
                text += "\n" + "Player Rope: " + infoData.PlayerRope;
                if (infoData.PlayerRope) text += " (x: " + infoData.PlayerRopeX + ")";

                text += "\n" + "Player Ledge: " + infoData.PlayerLedge;
                text += "\n" + "Player Wall: " + infoData.PlayerWall;
                text += "\n" + "Player Kicked: " + infoData.PlayerKicked;
                text += "\n";
                text += "\n" + "Jump State: " + infoData.PlayerJumpState;
                text += "\n" + "Jump Timer: " + infoData.PlayerJumpTimer;
                text += spacer;
            }

            if (filter.Value.HasFlag(InfotextFilter.DamageInfo)) {
                text += "\n" + "Tao Fruits: " + infoData.FruitCount;
                text += "\n" + "Attack: " + infoData.AttackDamage + " | Tali: " + infoData.FooDamage;
                text += spacer;
            }

            //if (showEnemyInfo) {
            //    text += "Enemy HP: " + infoData.enemyHP + "/" + infoData.enemyMaxHP + "\n";
            //    text += "Total HP DMG: " + infoData.totalHPDamage + "\n";
            //    text += "Total INT DMG: " + infoData.totalINTDamage + "\n";
            //    text += spacer;
            //}

            if (filter.Value.HasFlag(InfotextFilter.InteractableInfo)) {
                text += "\n" + "Interactables: ";
                if (infoData.Interactables.Count == 0) text += "None";
                else {
                    foreach (var interactable in infoData.Interactables) {
                        if (interactable is not "") text += "\n" + "  " + interactable;
                    }
                }

                text += spacer;
            }

            if (filter.Value.HasFlag(InfotextFilter.DebugInfo)) {
                text += "\n" + DebugInfo.GetInfoText();
            }
        } catch (Exception e) {
            Log.Error(e);
        }

        return text;
    }


    public void OnGui() {
        if (infotextActive) {
            Log.Info("info text is drawing");
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };
            styleBox ??= new GUIStyle(GUI.skin.box) { fontSize = 20 };

            const int itemHeight = 24;
            var visibleLines = debugCanvasInfoText.Count(c => c == '\n');
            var boxHeight = (visibleLines + 1) * itemHeight;

            const int boxWidth = 330;
            const int boxInset = 10;

            var boxRect = new Rect(10, 10, boxWidth, boxHeight);

            var oldColor = GUI.contentColor;
            var oldWrap = GUI.skin.box.wordWrap;
            GUI.contentColor = Color.white;
            GUI.skin.box.wordWrap = false;
            GUI.skin.box.fontSize = 20;
            GUI.skin.box.fontStyle = FontStyle.Bold;
            GUI.skin.box.alignment = TextAnchor.UpperLeft;
            GUI.Box(boxRect, debugCanvasInfoText);
            GUI.contentColor = oldColor;
            GUI.skin.box.wordWrap = oldWrap;
        }
    }

    private GUIStyle? style;
    private GUIStyle? styleBox;

    public void Destroy() {
        //UnityEngine.Object.Destroy(debugCanvasInfoText.gameObject);
    }
}