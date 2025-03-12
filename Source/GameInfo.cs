using System;
using UnityEngine;
using System.Collections.Generic;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace DebugModPlus;

public class GameInfo {
    private static float statsLastHP = 0;
    private static float statsLastIntDmg = 0;
    private static Vector2 statsLastPos = Vector2.zero;

    private static MovingAverage averagePlayerX = new();
    private static MovingAverage averagePlayerY = new();

    private float playerLastHP = statsLastHP;
    private float playerLastIntDmg = statsLastIntDmg;

    public string? ErrorText = null;

    public string GameLevel = "";
    public string CoreState = "";
    public string Cutscene = "";

    public float PlayerHp;
    public float PlayerLostHp;
    public float PlayerMaxHp;

    public float PlayerIntNewDmg;
    public float PlayerIntDmg;

    public Vector2 PlayerPos;

    public Vector2 PlayerSafePos;
    public Vector2 SceneRespawn;

    public Vector2 PlayerRespawn;

    public Vector2 PlayerSpeed;
    public Vector2 PlayerAvgSpeed;

    public string playerState = "";

    public string PlayerJumpState = "";
    public string PlayerJumpTimer = "";

    public bool PlayerRope;
    public float PlayerRopeX;
    public bool PlayerWall;
    public bool PlayerLedge;
    public bool PlayerKicked;

    public bool DebugAutoHeal;
    public bool DebugInvincibility;

    public float EnemyHP;

    public float CummHPDmg;
    public float CummIntDmg;

    public int FruitCount;

    public int Fruit;
    public int GreaterFruit;
    public int TwinFruit;

    //add other damage here
    public float AttackDamage;
    public float FooDamage;

    public List<string> Interactables = new();

    public GameInfo() {
        try {
            if (!GameCore.IsAvailable()) {
                return;
            }

            var core = GameCore.Instance;
            var player = core.player;

            if (player is null) {
                Log.Error("Error during InfoText collection: player or core is null");
                return;
            }

            GameLevel = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            CoreState = core.currentCoreState.ToString();
            if (core.currentCutScene) Cutscene = core.currentCutScene.name;

            var playerHealth = (PlayerHealth)player.GetHealth;
            if (playerHealth is not null) {
                PlayerMaxHp = playerHealth.maxHealth.Value;
                PlayerHp = playerHealth.CurrentHealthValue;

                if (PlayerHp != statsLastHP) {
                    PlayerLostHp = PlayerHp - statsLastHP;
                    statsLastHP = PlayerHp;
                }

                PlayerIntDmg = playerHealth.CurrentInternalInjury;

                if (PlayerIntDmg != playerLastIntDmg) {
                    PlayerIntNewDmg = PlayerIntDmg - playerLastIntDmg;
                }
            }

            if (core.gameLevel) {
                var nearestSpawnPoint = SingletonBehaviour<GameCore>.Instance.gameLevel.GetNearestSpawnPoint();
                var spawnType = nearestSpawnPoint.Item1;

                PlayerPos = player.transform.position;
                PlayerSafePos = player.lastSafeGroundPosition;

                //the ingame logic for this lol, its hidden in a state, not sure if theres another way to do this more clearly
                PlayerRespawn = SceneRespawn = nearestSpawnPoint.Item2;
                if (spawnType == PlayerSpawnPointManager.SpawnType.None) {
                    PlayerRespawn = PlayerSafePos;
                } else if (player.SafeGroundRecorder.LastSafeGroundPositionList.Count > 0 &&
                           (spawnType == PlayerSpawnPointManager.SpawnType.LastConnectionPoint ||
                            spawnType == PlayerSpawnPointManager.SpawnType.SavePoint)) {
                    PlayerRespawn = PlayerSafePos;
                } else {
                    var position2 = SingletonBehaviour<CameraManager>.Instance.cameraCore.theRealSceneCamera.transform
                        .position;
                    var num = Vector2.Distance(position2, SceneRespawn);
                    if (Vector2.Distance(position2, PlayerSafePos) < num) {
                        PlayerRespawn = PlayerSafePos;
                    }
                }
            }

            PlayerSpeed = player.FinalVelocity;

            //this is how kreon did it 
            averagePlayerX.Sample((long)PlayerSpeed.x);
            averagePlayerY.Sample((long)PlayerSpeed.y);
            PlayerAvgSpeed = new Vector2(averagePlayerX.GetAverageFloat, averagePlayerY.GetAverageFloat);

            playerState = player.CurrentState.name;

            //touching rope because of rope storage, climbing rope only works while the player is on it
            PlayerRope = player.touchingRope;
            if (PlayerRope) PlayerRopeX = player.touchingRope.transform.position.x;
            PlayerWall = player.isOnWall;
            PlayerLedge = player.isOnLedge;
            PlayerKicked = player.kicked;

            PlayerJumpState = player.jumpState.ToString();

            if (player.jumpState != Player.PlayerJumpState.None) {
                var varJumpTimer = player.currentVarJumpTimer;
                PlayerJumpTimer =
                    varJumpTimer > 0 ? varJumpTimer.ToString("0.00") : "";
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

            Fruit = ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item =>
                item is ItemData data && data.Title == "Tao Fruit")).ownNum.CurrentValue;
            GreaterFruit =
                ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item =>
                    item is ItemData data && data.Title == "Greater Tao Fruit")).ownNum.CurrentValue;
            TwinFruit = ((ItemData)GameConfig.Instance.allGameFlags.Flags.Find(item =>
                item is ItemData data && data.Title == "Twin Tao Fruit")).ownNum.CurrentValue;

            FruitCount = Fruit + GreaterFruit + TwinFruit;

            AttackDamage = player.normalAttackDealer.FinalValue;
            FooDamage = player.fooEffectDealer.FinalValue;
            //parry reflect
            //charge parry

            if (player.interactableFinder.CurrentInteractableArea is { } current) {
                foreach (var interaction in current.ValidInteractions) {
                    Interactables.Add(interaction.transform.parent.transform.parent.name);
                }
            }
        } catch (Exception ex) {
            ErrorText = ex.ToString();
            Log.Error(ErrorText);
        }
    }
}