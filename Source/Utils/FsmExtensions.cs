using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using MonsterLove.StateMachine;

namespace DebugModPlus.Utils;

public static class StateMachineExtensions {
    private static AccessTools.FieldRef<FSMStateMachineRunner, List<IStateMachine>> stateMachineRunnerStateMachineList =
        AccessTools.FieldRefAccess<FSMStateMachineRunner, List<IStateMachine>>("stateMachineList");

    private static AccessTools.FieldRef<AbstractStateTransition, AbstractConditionComp[]> stateTransitionConditions =
        AccessTools.FieldRefAccess<AbstractStateTransition, AbstractConditionComp[]>("conditions");


    public static AbstractConditionComp[] Conditions(this AbstractStateTransition transition) =>
        stateTransitionConditions.Invoke(transition);

    public static List<IStateMachine> GetMachines(this FSMStateMachineRunner runner) =>
        stateMachineRunnerStateMachineList.Invoke(runner);


    public static IEnumerable<(object, MappingState)> GetStates(this IStateMachine machine) {
        return machine.AccessField<object?>("_stateMapping")!
            .AccessProperty<IList>("getAllStates")!
            .Cast<object>()
            .Select(stateObj => {
                var state = stateObj.AccessField<object>("state");
                var stateBehaviour = stateObj.AccessField<MappingState>("stateBehavior");
                return (state, stateBehaviour);
            });
    }
}