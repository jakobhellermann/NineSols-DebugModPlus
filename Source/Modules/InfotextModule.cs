using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;
using NineSolsAPI;
using NineSolsAPI.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using BepInEx.Configuration;
using DebugModPlus.Savestates;

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

        All = GameInfo | BasicPlayerInfo | AdvancedPlayerInfo | RespawnInfo | DamageInfo | EnemyInfo | InteractableInfo
    }

    private static InfotextFilter defaultFilter = InfotextFilter.All;
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
            }

            else if ((RCGTime.timeScale > 0) || UIManager.Instance.PausePanelUI.isActiveAndEnabled) {
                text = UpdateInfoText();
            }

            else return;
        }
        debugCanvasInfoText = text;
    }

    private class InfoTextData {
        private static float statsLastHP = 0;
        private static float statsLastIntDmg = 0;
        private static Vector2 statsLastPos = Vector2.zero;

        private static MovingAverage averagePlayerX = new MovingAverage();
        private static MovingAverage averagePlayerY = new MovingAverage();

        private float playerLastHP = statsLastHP;
        private float playerLastIntDmg = statsLastIntDmg;

        public string? errorText = null;

        public string gameLevel = "";      
        public string coreState = "";
        public string cutscene = "";

        public float playerHP;
        public float playerLostHP;
        public float playerMaxHP;

        public float playerIntNewDmg;
        public float playerIntDmg;   

        public Vector2 playerPos; 

        public Vector2 playerSafePos; 
        public Vector2 sceneRespawn;  

        public Vector2 playerRespawn;

        public Vector2 playerSpeed;    
        public Vector2 playerAvgSpeed; 

        public string playerState = "";

        public string playerJumpState = "";
        public string playerJumpTimer = "";

        public bool playerRope;
        public float playerRopeX;
        public bool playerWall;
        public bool playerLedge;
        public bool playerKicked;

        public bool debugAutoHeal;
        public bool debugInvincibility;

        public float EnemyHP;

        public float CummHPDmg;       
        public float CummIntDmg;      

        public int fruitCount; 

        public int fruit;             
        public int greaterFruit;      
        public int twinFruit;         

        //add other damage here
        public float attackDamage;    
        public float fooDamage;       

        public List<string> interactables = new List<string>();

        public InfoTextData() {
            try {
                if (!SingletonBehaviour<GameCore>.IsAvailable()) {
                    return;
                }

                var core = SingletonBehaviour<GameCore>.Instance;
                var player = core.player;

                if (player is null || core is null) {
                    Log.Error("Error during InfoText collection: player or core is null");
                    return;
                }
                gameLevel = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                coreState = core.currentCoreState.ToString();
                if (core.currentCutScene) cutscene = core.currentCutScene.name;

                PlayerHealth playerHealth = (PlayerHealth)player.GetHealth;
                if (playerHealth is not null) {
                    playerMaxHP = playerHealth.maxHealth.Value;
                    playerHP = playerHealth.CurrentHealthValue;

                    if (playerHP != statsLastHP) {
                        playerLostHP = playerHP - statsLastHP;
                        statsLastHP = playerHP;
                    }

                    playerIntDmg = playerHealth.CurrentInternalInjury;

                    if (playerIntDmg != playerLastIntDmg) {
                        playerIntNewDmg = playerIntDmg - playerLastIntDmg;
                    }
                }
                if (core.gameLevel) {
                    ValueTuple<PlayerSpawnPointManager.SpawnType, Vector3> nearestSpawnPoint = SingletonBehaviour<GameCore>.Instance.gameLevel.GetNearestSpawnPoint();
                    PlayerSpawnPointManager.SpawnType spawnType = nearestSpawnPoint.Item1;

                    playerPos = player.transform.position;
                    playerSafePos = player.lastSafeGroundPosition;

                    //the ingame logic for this lol, its hidden in a state, not sure if theres another way to do this more clearly
                    playerRespawn = sceneRespawn = nearestSpawnPoint.Item2;
                    if (spawnType == PlayerSpawnPointManager.SpawnType.None) {
                        playerRespawn = playerSafePos;
                    }
                    else if (player.SafeGroundRecorder.LastSafeGroundPositionList.Count > 0 && (spawnType == PlayerSpawnPointManager.SpawnType.LastConnectionPoint || spawnType == PlayerSpawnPointManager.SpawnType.SavePoint)) {
                        playerRespawn = playerSafePos;
                    }
                    else {
                        Vector3 position2 = SingletonBehaviour<CameraManager>.Instance.cameraCore.theRealSceneCamera.transform.position;
                        float num = Vector2.Distance(position2, sceneRespawn);
                        if (Vector2.Distance(position2, playerSafePos) < num) {
                            playerRespawn = playerSafePos;
                        }
                    }
                }
                playerSpeed = player.FinalVelocity;

                //this is how kreon did it 
                averagePlayerX.Sample((long)playerSpeed.x);
                averagePlayerY.Sample((long)playerSpeed.y);
                playerAvgSpeed = new Vector2(averagePlayerX.GetAverageFloat, averagePlayerY.GetAverageFloat);

                playerState = player.CurrentState.name;

                //touching rope because of rope storage, climbing rope only works while the player is on it
                playerRope = player.touchingRope;
                if (playerRope) playerRopeX = player.touchingRope.transform.position.x;
                playerWall = player.isOnWall;
                playerLedge = player.isOnLedge;
                playerKicked = player.kicked;

                playerJumpState = player.jumpState.ToString();

                if (player.jumpState != Player.PlayerJumpState.None) {
                    var varJumpTimer = player.currentVarJumpTimer;
                    playerJumpTimer =
                         (varJumpTimer > 0 ? varJumpTimer.ToString("0.00") : "");
                }

                //need to implement cheats
                /*
                debugAutoHeal;
                debugInvincibility;
                */

                //need to implement hooks
                /*
                EnemyHP; 

                CummHPDmg;       //hidden
                CummIntDmg;      //hidden
                */

                fruit = ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item => item is ItemData data && data.Title == "Tao Fruit")).ownNum.CurrentValue;
                greaterFruit = ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item => item is ItemData data && data.Title == "Greater Tao Fruit")).ownNum.CurrentValue;
                twinFruit = ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item => item is ItemData data && data.Title == "Twin Tao Fruit")).ownNum.CurrentValue;

                fruitCount = fruit + greaterFruit + twinFruit;

                attackDamage = player.normalAttackDealer.FinalValue;
                fooDamage = player.fooEffectDealer.FinalValue;
                //parry reflect
                //charge parry

                if (player.interactableFinder.CurrentInteractableArea is { } current) {
                    foreach (var interaction in current.ValidInteractions) {
                        interactables.Add(interaction.transform.parent.transform.parent.name);
                    }
                }
            }

            catch (Exception ex) {
                errorText = ex.ToString();
                Log.Error(errorText);
            }
        }
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
        var text = "[DEBUG+] (v" + Assembly.GetExecutingAssembly().GetName().Version.ToString()+")";
        const string spacer = "\n----------------------------------------------"; 




        var infoData = new InfoTextData();
        if (infoData.errorText is not null) text += "\n" + infoData.errorText;
        text += spacer;
        if (filter.Value.HasFlag(InfotextFilter.GameInfo)) {
            text += "\n" + infoData.gameLevel;
            text += "\n" + "Game State: " + infoData.coreState;
            text += "\n" + "Cutscene: " + infoData.cutscene;
            text += spacer;
        }

        if (filter.Value.HasFlag(InfotextFilter.BasicPlayerInfo)) {
            text += "\n" + "HP: " + infoData.playerHP + "/" + infoData.playerMaxHP + " (" + infoData.playerLostHP + ")";
            text += "\n" + "Int Dmg: " + infoData.playerIntDmg + " (" + infoData.playerIntNewDmg + ")";
            text += "\n";
            text += "\n" + "Position: " + infoData.playerPos;
            text += "\n" + "Speed: " + infoData.playerSpeed;
            text += "\n" + "Avg Speed: " + infoData.playerAvgSpeed;
            text += spacer;
        }

        if (filter.Value.HasFlag(InfotextFilter.RespawnInfo)) {
            text += "\n" + "Spawn Pos: " + infoData.playerRespawn;
            text += "\n" + "Safe Pos: " + infoData.playerSafePos;
            text += "\n" + "Scene Pos: " + infoData.sceneRespawn;
            text += spacer;
        }

        if (filter.Value.HasFlag(InfotextFilter.AdvancedPlayerInfo)) {
            text += "\n" + "Player State: " + infoData.playerState;
            text += "\n" + "Player Rope: " + infoData.playerRope;
              if (infoData.playerRope) text += " (x: " + infoData.playerRopeX + ")";

            text += "\n" + "Player Ledge: " + infoData.playerLedge;
            text += "\n" + "Player Wall: " + infoData.playerWall;
            text += "\n" + "Player Kicked: " + infoData.playerKicked;
            text += "\n";
            text += "\n" + "Jump State: " + infoData.playerJumpState;
            text += "\n" + "Jump Timer: " + infoData.playerJumpTimer;
            text += spacer;
        }

        if (filter.Value.HasFlag(InfotextFilter.DamageInfo)) {
            text += "\n" + "Tao Fruits: " + infoData.fruitCount;
            text += "\n" + "Attack: " + infoData.attackDamage + " | Tali: " + infoData.fooDamage;
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
            if (infoData.interactables.Count == 0) text += "None";
            else {
                foreach (string interactable in infoData.interactables) {
                    if (interactable is not "") text += "\n" + "  " + interactable;
                }
            }
        }

        return text;
    }

    
    public void OnGui() {
        if (infotextActive) {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };
            styleBox ??= new GUIStyle(GUI.skin.box) { fontSize = 20 };

            const int itemHeight = 24;
            var visibleLines = debugCanvasInfoText.Count(c => c == '\n');
            var boxHeight = (visibleLines+1) * itemHeight;

            const int boxWidth = 330;
            const int boxInset = 10;

            var boxRect = new Rect(10, 10, boxWidth, boxHeight);

            Color oldColor = GUI.contentColor;
            bool oldWrap = GUI.skin.box.wordWrap;
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