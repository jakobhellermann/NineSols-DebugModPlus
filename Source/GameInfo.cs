using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DebugModPlus;
using HarmonyLib;
using MonsterLove.StateMachine;
using NineSolsAPI;
using NineSolsAPI.Utils;
using UnityEngine;

namespace TAS;

public static class GameInfo {
    private const bool ShowClipInfo = true;

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

            (bool, string)[] flags = [
                (player.isOnWall, "Wall"),
                (player.isOnLedge, "Ledge"),
                (player.isOnRope, "Rope"),
                (player.kicked, "Kicked"),
                (player.onGround, "OnGround"),
                (player.interactableFinder.CurrentInteractableArea, "CanInteract"),
                (player.rollCooldownTimer <= 0, "CanDash"),
                (player.airJumpCount > 0, "AirJumping"),
            ];
            (float, string)[] timers = [
                (player.rollCooldownTimer, "DashCD"),
                (player.jumpGraceTimer, "Coyote"),
            ];
            text += Flags(flags, timers);

            if (player.jumpState != Player.PlayerJumpState.None) {
                var varJumpTimer = player.currentVarJumpTimer;
                var groundReference = player.AccessField<float>("GroundJumpRefrenceY");
                var height = player.transform.position.y - groundReference;
                text +=
                    $"JumpState {player.jumpState} {(varJumpTimer > 0 ? varJumpTimer.ToString("0.00") + " " : "")}h={height}\n";
            } else text += "\n";

            text += AnimationText(player.animator, includeRapidlyChanging);
        }

        var playerNymphState =
            (PlayerHackDroneControlState)player.fsm.FindMappingState(PlayerStateType.HackDroneControl);
        var nymph = playerNymphState.hackDrone;

        if (nymph.fsm != null && nymph.fsm.State != HackDrone.DroneStateType.Init) {
            text += $"\nNymph {nymph.fsm.State}\n";
            text += $"  Position: {(Vector2)nymph.transform.position}\n";
            text += $"  Speed: {(Vector2)nymph.droneVel}\n";
            text += "  " + Flags([
                    (nymph.AccessField<bool>("isDashCD"), "DashCD"),
                    (nymph.AccessField<bool>("isOutOfRange"), "OutOfRange"),
                ],
                [
                    (nymph.AccessField<float>("OutOfRangeTimer"), "OutOfRange"),
                ],
                " "
            );
            text += AnimationText(nymph.animator, includeRapidlyChanging) + "\n";
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

    private static string Flags(IEnumerable<(bool, string)> flags, IEnumerable<(float, string)> timers,
        string sep = "\n") {
        var flagsStr = flags.Where(x => x.Item1).Join(x => x.Item2, " ");
        var timersStr = timers.Where(x => x.Item1 > 0).Join(x => $"{x.Item2}({x.Item1:0.000})", " ");

        return $"{flagsStr}{sep}{timersStr}\n";
    }

    private static string AnimationText(Animator animator, bool includeRapidlyChanging) {
        var animInfo = animator.GetCurrentAnimatorStateInfo(0);
        var animName = animator.ResolveHash(animInfo.m_Name);
        var text = $"Animation {animName}";
        if (includeRapidlyChanging) {
            text += $" {animInfo.normalizedTime % 1 * 100:00.0}%";
        }

        return $"{text}\n";
    }

    public static string GetMonsterInfotext() {
        var text = "";
        foreach (var monster in MonsterManager.Instance.monsterDict.Values) {
            if (!monster.isActiveAndEnabled) continue;

            text += GetMonsterInfotext(monster) + "\n";
        }

        return text;
    }

    public static string GetMonsterInfotext(MonsterBase monster) {
        var text = "";
        text += MonsterName(monster) + "\n";

        var core = monster.monsterCore;

        if (core.attackSequenceMoodule.getCurrentSequence() is not null) {
            text += "TODO: attack sequence\n";
        }

        var state = monster.fsm.FindMappingState(monster.fsm.State);

        var animInfo = monster.animator.GetCurrentAnimatorStateInfo(0);
        text += $"State: {FsmStateName(state)}";
        text += $" {animInfo.normalizedTime % 1 * 100:00}%";
        text += "\n";

        if (state is BossGeneralState bgs) {
            if (bgs.attackQueue) {
                text += "TODO: attackQueue\n";
            }

            text += "Queue:\n";
            foreach (var attack in bgs.QueuedAttacks) {
                text += $"- {FsmStateName(monster.fsm, attack)}\n";
            }

            if (bgs.clip != null && ShowClipInfo) {
                text += "Clip:\n";
                text += $"- {animInfo.normalizedTime:0.00}\n";
                foreach (var clip in bgs.clip.events) {
                    var e = (AnimationEvents.AnimationEvent)clip.intParameter;
                    text += $"- {clip.time:0.00}: {e}\n";
                }
            }
        }

        var hurtInterrupt = monster.HurtInterrupt;
        if (hurtInterrupt.isActiveAndEnabled) {
            var th = hurtInterrupt.AccessField<float>("AccumulateDamageTh");
            if (th > 0) {
                text +=
                    $"Hurt Interrupt: {hurtInterrupt.currentAccumulateDamage / monster.postureSystem.FullPostureValue:0.00} > {th}\n";
            }
        }

        var canCrit = (bool)typeof(MonsterBase)
            .GetMethod("MonsterStatCanCriticalHit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(monster, []);
        if (canCrit) {
            if (monster.IsEngaging) text += "IsEngaging\n";
        }

        return text;
    }

    private static string FsmStateName(MappingState mappingState) => ReCjk.Replace(mappingState.name, "").Trim();

    private static string FsmStateName(StateMachine<MonsterBase.States> monster, MonsterBase.States state) {
        if (monster.FindMappingState(state) is { } mappingState) {
            return FsmStateName(mappingState);
        }

        return state.ToString();
    }

    private static readonly Regex ReCjk = new(@"_?\p{IsCJKUnifiedIdeographs}+|^\d+_");
    // private static readonly Regex ReCjk = new(@"_?\p{IsCJKUnifiedIdeographs}+|\[\d+\] ?|^\d+_");

    private static string MonsterName(MonsterBase monster) {
        var field = typeof(MonsterBase).GetField("_monsterStat") ?? typeof(MonsterBase).GetField("monsterStat");
        var stat = (MonsterStat)field.GetValue(monster);
        var monsterName = stat.monsterName.ToString();

        if (monsterName != "") return monsterName;

        return monster.name.TrimStartMatches("StealthGameMonster_").TrimStartMatches("TrapMonster_")
            .TrimEndMatches("(Clone)")
            .ToString();
    }
}