using System;
using NineSolsAPI;
using TAS;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DebugModPlus;
using HarmonyLib;
using NineSolsAPI.Utils;
using UnityEngine.UIElements;
using BepInEx.Configuration;
using DebugModPlus.Savestates;

namespace DebugModPlus
{
    public class GameInfo {
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

          public GameInfo() {
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
}
