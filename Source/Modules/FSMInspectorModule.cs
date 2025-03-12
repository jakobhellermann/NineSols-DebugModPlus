using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DebugModPlus.Utils;
using HarmonyLib;
using MonsterLove.StateMachine;
using NineSolsAPI;
using NineSolsAPI.Utils;
using RCGFSM.Animation;
using RCGFSM.GameObjects;
using RCGFSM.StateEvents;
using RCGFSM.Transition;
using RCGFSM.Variable;
using UnityEngine;
using UnityEngine.Events;

namespace DebugModPlus.Modules;

[HarmonyPatch]
public class FsmInspectorModule {
    private static GUIStyle? style;

    private string? text;

    private static string InspectFsmMonsterLove(FSMStateMachineRunner runner) {
        var text = "";
        var machines = runner.GetMachines();

        var mb = runner.GetComponent<MonsterBase>();
        if (mb) text += $"\nMonster animation state {mb.currentPlayingAnimatorState}\n\n";

        foreach (var machine in machines) {
            var stateType = machine.GetType().GenericTypeArguments[0];
            text += $"State type: {stateType}\n";

            var currentState = machine.CurrentStateMap.stateObj;
            text += $"Current state: {currentState}\n";

            foreach (var (state, stateBehaviour) in machine.GetStates()) {
                text += $"  {state} ({stateBehaviour.GetType().Name})\n";

                if (stateBehaviour is MonsterState monsterState) {
                    text += $"    Exit State: {monsterState.exitState}\n";

                    if (stateBehaviour is StealthPreAttackState pre) {
                        text += $"    Approaching Scheme Index {pre.SchemesIndex}\n";
                        for (var i = 0; i < pre.ApproachingSchemes.Count; i++) {
                            var approaching = pre.ApproachingSchemes[i];
                            text += $"    Approaching Scheme {i}: {approaching.name}\n";
                            text += $"      exit range: {approaching.ExitApproachingRange}\n";
                        }
                    }

                    var stateActions =
                        AccessBaseField<AbstractStateAction[]>(stateBehaviour, typeof(MonsterState), "stateActions");
                    if (stateActions.Length > 0) text += "HAS STATE ACTIONS\n";
                    // text += $"    {monsterState.AccessField<AbstractStateAction[]>("stateActions")}\n";
                    // ToastManager.Toast(field==null);
                    // text += $"    {monsterState.AccessField<AbstractStateAction[]>("stateActions")}\n";
                }
            }
        }

        return text;
    }

    private string StateName(string name) => name.TrimStartMatches("[State] ").ToString();

    private static string VariableName(AbstractVariable? variable) {
        if (variable == null) return "null";

        var name = variable.ToString().TrimStartMatches("[Variable] ")
            .TrimEndMatches(" (VariableBool)").ToString();

        return $"{name} {variable.FinalData?.GetSaveID}";
    }

    private string TransitionName(AbstractStateTransition transition) {
        if (!transition) return "null";

        return transition.name.TrimStartMatches("[Action] ").ToString()
            .TrimStartMatches("[Transition] ").ToString();
    }

    private bool hideAnimationTransitions = true;
    private bool hideDefaultTransitions = true;
    private static bool showFlagIds = true;

    private bool UnityEventHasCalls(UnityEvent e) => e.GetPersistentEventCount() > 0 || e.m_Calls.Count > 0;

    private string InspectFSM(GameObject gameObject) {
        if (!gameObject) return "";

        // RCGFlagFetcher
        // RCGArgEventBinder
        // StateMachineOwner
        var text = "";

        var runner = gameObject.GetComponent<FSMStateMachineRunner>();
        if (runner) return InspectFsmMonsterLove(runner);

        var owner = gameObject.GetComponent<StateMachineOwner>();
        if (!owner) return "No fsm found";

        var context = owner.FsmContext;

        if (context.fsm == null) {
            text += "fsm is null?";
            return text;
        }

        text += $"Current State: {context.fsm.State.gameObject.name.TrimStartMatches("[State] ").ToString()}\n";
        text += $"Last transition: {TransitionName(context.LastTransition)}\n";
        foreach (var state in context.States) {
            text += $"State {StateName(state.name)}:\n";
            text += "  Actions:\n";
            foreach (var action in state.Actions) {
                // just calls transitioncheck for its AbstractStateTransition
                if (action is StateTransitionAction) continue;

                var actionStr = action.ToString();

                if (action is AnimatorPlayAction animAction) {
                    if (hideAnimationTransitions) continue;

                    actionStr = $"play animation {animAction.StateName} on '{animAction.animator?.name}'";
                } else if (action is GameObjectActivateAction activateAction) {
                    actionStr =
                        $"{activateAction.name.TrimStartMatches("[Action] ").ToString()}";
                    if (activateAction.enableObj is { Count: > 0 }) {
                        actionStr +=
                            $", enable object{(activateAction.enableObj.Count > 1 ? "s" : "")} {activateAction.enableObj.Select(x => $"'{x.name}'").Join()}";
                    }

                    if (activateAction.disableObj is { Count: > 0 }) {
                        actionStr +=
                            $", disable object{(activateAction.disableObj.Count > 1 ? "s" : "")} {activateAction.disableObj.Select(x => $"'{x.name}'").Join()}";
                    }
                } else if (action is StateEventAction stateEventAction) {
                    var hasEnter = UnityEventHasCalls(stateEventAction.OnStateEnterEvent);
                    var hasUpdate = UnityEventHasCalls(stateEventAction.OnStateUpdateEvent);
                    var hasExit = UnityEventHasCalls(stateEventAction.OnStateExitEvent);

                    actionStr =
                        $"EventAction {(hasEnter ? "Enter " : "")}{(hasUpdate ? "Update " : "")}{(hasExit ? "Exit " : "")} {(!hasEnter && !hasUpdate && !hasExit ? "<empty>" : "")}";
                } else if (action is SetVariableBoolAction setVarBoolAction) {
                    if (setVarBoolAction.Multiple) {
                        // todo
                    } else {
                        actionStr =
                            $"set bool {VariableName(setVarBoolAction.targetFlag)} = {setVarBoolAction.TargetValue}";
                    }
                }

                if (action.GetComponent<RCGEventSender>() is { } sender) {
                    actionStr +=
                        $"sending event {sender.eventType?.ToString().TrimEndMatches(" (RCGEventType)").ToString()} to {sender.bindReceivers?.Count} receivers";
                }

                if (!action.isActiveAndEnabled) actionStr = $"(disabled) {actionStr}";

                text += $"    {actionStr}\n";
            }


            var transitionHeaderOnce = true;
            foreach (var transition in state.Transitions) {
                if (hideDefaultTransitions && transition.IsDefaultTransition) continue;

                if (transitionHeaderOnce) {
                    text += "  Transitions:\n";
                    transitionHeaderOnce = false;
                }

                var transitionName = transition.name.TrimStartMatches("[Action] ").ToString()
                    .TrimStartMatches("[Transition] ");

                text +=
                    $"    {(transition.IsDefaultTransition ? "default" : "")} to {StateName(transition.target.name)} ({transitionName.ToString()})\n";
                foreach (var condition in transition.Conditions()) {
                    var conditionStr = ConditionStr(condition);
                    text += $"      {conditionStr}\n";
                }
            }
        }

        return text;
    }

    public static string ConditionStr(AbstractConditionComp condition) {
        var name = condition.name.TrimStartMatches("[Condition] ").ToString();
        var conditionStr = "";

        switch (condition) {
            case FlagBoolCondition boolCondition: {
                conditionStr =
                    $"{name} bool flag {VariableName(boolCondition.flagBool)} current {boolCondition.flagBool?.FlagValue}";
                if (showFlagIds) conditionStr += $" {boolCondition.flagBool?.boolFlag?.FinalSaveID}";
                if (showFlagIds) conditionStr += $" {boolCondition.flagBool}";
                break;
            }
            case GeneralCondition generalCondition: {
                if (showFlagIds) conditionStr += $" {generalCondition}";
                break;
            }
            case PlayerMovePredictCondition movePredictCondition: {
                conditionStr += "Player";
                if (movePredictCondition.ParryDetect) conditionStr += " Parry";
                if (movePredictCondition.DodgeDetect) conditionStr += " Dash";
                if (movePredictCondition.JumpDetect) conditionStr += " Jump";
                if (movePredictCondition.InAirDetect) conditionStr += " InAir";
                if (movePredictCondition.AttackDetect) conditionStr += " Attack";
                if (movePredictCondition.ThirdAttackDetect) conditionStr += " Third";
                if (movePredictCondition.ChargeAttackDetect) conditionStr += " Charged";
                if (movePredictCondition.FooDetect) conditionStr += " Foo";
                if (movePredictCondition.ArrowDetect) conditionStr += " Shoots";
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (movePredictCondition.randomChance != 1.0)
                    conditionStr += $" at {movePredictCondition.randomChance * 100:0}%";
                break;
            }
            default: {
                if (showFlagIds) conditionStr += $" {condition.GetType()}";
                break;
            }
        }

        if (condition.FinalResultInverted) conditionStr = $"(inverted) {conditionStr}";
        return $"({(condition.FinalResult ? "true" : "false")}) {conditionStr}";
    }

    public List<GameObject> Objects = [];

    public void OnGui() {
        var extra = new GameObject[] { };
        // { GameObject.Find("A2_S5_ BossHorseman_GameLevel/Room/StealthGameMonster_SpearHorseMan") };

        try {
            text = "";
            foreach (var obj in Objects.Concat(extra)) {
                if (!obj) {
                    text += "\n";
                    continue;
                }

                text += $"{obj.name}\n";
                text += "  " + ObjectUtils.ObjectPath(obj);
                text += "\n";
                try {
                    text += InspectFSM(obj);
                } catch (Exception e) {
                    text += e.ToString();
                }

                text += "\n";
            }
        } catch (Exception e) {
            ToastManager.Toast(e);
        }

        text = text?.Trim();

        const int padding = 8;
        if (style == null) {
            style = new GUIStyle(GUI.skin.label) {
                fontSize = 20,
                normal = {
                    background = UiUtils.GetColorTexture(new Color(0f, 0f, 0f, 0.5f)),
                },
            };
        }

        var size = style.CalcSize(new GUIContent(text));

        GUI.Label(new Rect(padding, padding, size.x, size.y), text, style);
    }

    private static T AccessBaseField<T>(object val, Type baseType, string fieldName) =>
        (T)baseType
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(val);
}