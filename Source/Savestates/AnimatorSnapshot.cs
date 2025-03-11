using NineSolsAPI;

namespace DebugModPlus;

using System.Collections.Generic;
using UnityEngine;

public class AnimatorSnapshot {
    public required int StateHash;
    public required float NormalizedTime;
    public required Dictionary<int, float> ParamsFloat;
    public required Dictionary<int, int> ParamsInt;
    public required Dictionary<int, bool> ParamsBool;

    public static AnimatorSnapshot Snapshot(Animator animator) {
        var currentState = animator.GetCurrentAnimatorStateInfo(0);

        AnimatorControllerParameter[] parameters = animator.parameters;
        var paramsFloat = new Dictionary<int, float>();
        var paramsBool = new Dictionary<int, bool>();
        var paramsInt = new Dictionary<int, int>();

        foreach (var param in parameters) {
            switch (param.type) {
                case AnimatorControllerParameterType.Float:
                    paramsFloat[param.nameHash] = animator.GetFloat(param.nameHash);
                    break;
                case AnimatorControllerParameterType.Bool:
                    paramsBool[param.nameHash] = animator.GetBool(param.nameHash);
                    break;
                case AnimatorControllerParameterType.Int:
                    paramsInt[param.nameHash] = animator.GetInteger(param.nameHash);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    continue;
                default:
                    ToastManager.Toast($"Unsnapshotted param {param.type}");
                    break;
            }
        }

        return new AnimatorSnapshot {
            StateHash = currentState.fullPathHash,
            NormalizedTime = currentState.normalizedTime,
            ParamsFloat = paramsFloat,
            ParamsInt = paramsInt,
            ParamsBool = paramsBool,
        };
    }

    public void Restore(Animator animator) {
        if (animator == null) return;


        if (animator.name == "Animator") {
            // HACK: think about this
            for (var t = animator.transform; t; t = t.parent) {
                t.gameObject.SetActive(true);
            }
        }


        animator.Play(StateHash, 0, NormalizedTime);
        animator.Update(0);
        foreach (var param in ParamsFloat) animator.SetFloat(param.Key, param.Value);
        foreach (var param in ParamsInt) animator.SetInteger(param.Key, param.Value);
        foreach (var param in ParamsBool) animator.SetBool(param.Key, param.Value);
    }
}