#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
using IStateMachine = MonsterLove.StateMachine.IStateMachine;

namespace DebugModPlus.Modules;

[HarmonyPatch]
public class FsmInspectorModule {
    private static GUIStyle? style;

    private string? text = null;

    private static AccessTools.FieldRef<FSMStateMachineRunner, List<IStateMachine>> stateMachineRunnerStateMachineList =
        AccessTools.FieldRefAccess<FSMStateMachineRunner, List<IStateMachine>>("stateMachineList");

    private static AccessTools.FieldRef<AbstractStateTransition, AbstractConditionComp[]> stateTransitionConditions =
        AccessTools.FieldRefAccess<AbstractStateTransition, AbstractConditionComp[]>("conditions");

    private string InspectFSMMonsterLove(FSMStateMachineRunner runner) {
        var text = "";
        var machines = stateMachineRunnerStateMachineList.Invoke(runner);
        foreach (var machine in machines) {
            var stateType = machine.GetType().GenericTypeArguments[0];

            var currentState = machine.CurrentStateMap;

            text += $"State type: {stateType}\n";
            text += $"Current state: {currentState.stateObj}\n";
        }

        return text;
    }

    private string StateName(string name) => name.TrimStartMatches("[State] ").ToString();

    private string VariableName(AbstractVariable? variable) {
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
    private bool showFlagIds = true;

    private bool UnityEventHasCalls(UnityEvent e) => e.GetPersistentEventCount() > 0 || e.m_Calls.Count > 0;

    private string InspectFSM(GameObject gameObject) {
        if (!gameObject) return "null";

        // RCGFlagFetcher
        // RCGArgEventBinder
        // StateMachineOwner
        var text = "";

        var runner = gameObject.GetComponent<FSMStateMachineRunner>();
        if (runner) return InspectFSMMonsterLove(runner);

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
                    if (activateAction.enableObj is { Count: > 0 })
                        actionStr +=
                            $", enable object{(activateAction.enableObj.Count > 1 ? "s" : "")} {activateAction.enableObj.Select(x => $"'{x.name}'").Join()}";
                    if (activateAction.disableObj is { Count: > 0 })
                        actionStr +=
                            $", disable object{(activateAction.disableObj.Count > 1 ? "s" : "")} {activateAction.disableObj.Select(x => $"'{x.name}'").Join()}";
                } else if (action is StateEventAction stateEventAction) {
                    var hasEnter = UnityEventHasCalls(stateEventAction.OnStateEnterEvent);
                    var hasUpdate = UnityEventHasCalls(stateEventAction.OnStateUpdateEvent);
                    var hasExit = UnityEventHasCalls(stateEventAction.OnStateExitEvent);

                    actionStr =
                        $"EventAction {(hasEnter ? "Enter " : "")}{(hasUpdate ? "Update " : "")}{(hasExit ? "Exit " : "")} {(!hasEnter && !hasUpdate && !hasExit ? "<empty>" : "")}";
                } else if (action is SetVariableBoolAction setVarBoolAction) {
                    if (setVarBoolAction.Multiple) {
                        // todo
                    } else
                        actionStr =
                            $"set bool {VariableName(setVarBoolAction.targetFlag)} = {setVarBoolAction.TargetValue}";
                }

                if (action.GetComponent<RCGEventSender>() is { } sender)
                    actionStr +=
                        $"sending event {sender.eventType.ToString().TrimEndMatches(" (RCGEventType)").ToString()} to {sender.bindReceivers.Count} receivers";

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
                foreach (var condition in stateTransitionConditions.Invoke(transition)) {
                    var conditionStr = condition.name.TrimStartMatches("[Condition] ").ToString();
                    if (condition is FlagBoolCondition boolCondition) {
                        conditionStr =
                            $"bool flag {VariableName(boolCondition.flagBool)} current {boolCondition.flagBool?.FlagValue}";
                        if (showFlagIds) conditionStr += $" {boolCondition.flagBool?.boolFlag?.FinalSaveID}";
                        if (showFlagIds) conditionStr += $" {boolCondition.flagBool}";
                    }

                    if (condition.FinalResultInverted) conditionStr = $"!{conditionStr}";


                    text += $"      {conditionStr}\n";
                }
            }
        }

        return text;
    }

    public List<GameObject> Objects = [];

    public void OnGui() {
        // if (text is null) {
        // ToastManager.Toast(Screen.fullScreenMode);

        // var obj = GameObject.Find("A7_S1/Room/Prefab/PhoneCallFSM_家裡古樹暴走 潩獮䑜浥獹楴祦䕜桮湡散");
        // var obj = GameObject.Find("A7_S1/Room/Prefab/A7_S1_三階段FSM    ");
        // var obj = GameObject.Find("GameCore(Clone)/RCG LifeCycle/PPlayer");
        // GameObject.Find(
        //     "AG_S2/Room/Prefab/Treasure Chests 寶箱/LootProvider 刺蝟玉/[Mech]SealedBoxTreasure FSM Interactable Variant"),
        // GameObject.Find(
        //     "AG_S2/Room/Prefab/Treasure Chests 寶箱/LootProvider 刺蝟玉/0_DropPickable Bag FSM/ItemProvider/DropPickable FSM Prototype"),
        try {
            text = "";
            foreach (var obj in Objects) {
                if (!obj) {
                    text += "null\n";
                    continue;
                }

                text += $"{obj.name}\n";
                text += ObjectUtils.ObjectPath(obj);
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

        const int padding = 8;

        style ??= new GUIStyle(GUI.skin.label) { fontSize = 20 };
        GUI.Label(new Rect(padding, padding, 6000, 1000), text, style);
    }
}