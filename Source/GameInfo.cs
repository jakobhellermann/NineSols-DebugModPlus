using System.Collections.Generic;
using System.Linq;
using DebugModPlus;
using HarmonyLib;
using UnityEngine;

namespace TAS;

public static class GameInfo {
    public static string GetInfoText(bool includeRapidlyChanging = false) {
        var text = "";

        if (!ApplicationCore.IsAvailable()) return "Loading";

        if (!GameCore.IsAvailable()) {
            text += "MainMenu\n";
            text += PlayerInputBinder.Instance.currentStateType.ToString();
            return text;
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
            text += $"HP: {player.health.CurrentHealthValue:0.00} (+{player.health.CurrentInternalInjury:0.00})\n";
            var state = typeof(PlayerStateType).GetEnumName(player.fsm.State);
            var inputState = player.playerInput.fsm.State;
            text += $"State: {state} {(inputState == PlayerInputStateType.Action ? "" : inputState.ToString())}\n";

            List<(bool, string)> flags = [
                (player.isOnWall, "Wall"),
                (player.isOnLedge, "Ledge"),
                (player.isOnRope, "Rope"),
                (player.kicked, "Kicked"),
                (player.onGround, "OnGround"),
                (player.interactableFinder.CurrentInteractableArea, "CanInteract"),
                (player.rollCooldownTimer <= 0, "CanDash"),
                (player.airJumpCount > 0, "AirJumping"),
            ];
            List<(float, string)> timers = [
                (player.rollCooldownTimer, "DashCD"),
                (player.jumpGraceTimer, "Coyote"),
            ];

            var flagsStr = flags.Where(x => x.Item1).Join(x => x.Item2, " ");
            var timersStr = timers.Where(x => x.Item1 > 0).Join(x => $"{x.Item2}({x.Item1:0.000})", " ");

            text += $"{flagsStr}\n{timersStr}\n";

            if (player.jumpState != Player.PlayerJumpState.None) {
                var varJumpTimer = player.currentVarJumpTimer;
                var groundReference = player.AccessField<float>("GroundJumpRefrenceY");
                var height = player.transform.position.y - groundReference;
                text +=
                    $"JumpState {player.jumpState} {(varJumpTimer > 0 ? varJumpTimer.ToString("0.00") + " " : "")}h={height}\n";
            } else text += "\n";

            var animInfo = player.animator.GetCurrentAnimatorStateInfo(0);
            var animName = player.animator.ResolveHash(animInfo.m_Name);
            text += $"Animation {animName}";
            if (includeRapidlyChanging) {
                text += $" {animInfo.normalizedTime % 1 * 100:00.0}%";
            }

            text += "\n";
        }

        var currentLevel = core.gameLevel;
        if (currentLevel) {
            text += $"[{currentLevel.SceneName}] ({currentLevel.BlockCountX}x{currentLevel.BlockCountY})";
            if (includeRapidlyChanging) {
                text += $" dt={Time.deltaTime:0.00000000}\n";
            }

            text += "\n";
        }

        if (core.currentCutScene) text += $"{core.currentCutScene}";

        return text;
    }
}