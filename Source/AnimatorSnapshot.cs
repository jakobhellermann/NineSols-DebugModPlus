using NineSolsAPI;

namespace DebugModPlus;

using System.Collections.Generic;
using UnityEngine;

public class AnimatorSnapshot {
    public required int stateHash;
    public required float normalizedTime;
    public required Dictionary<int, float> paramsFloat;
    public required Dictionary<int, int> paramsInt;
    public required Dictionary<int, bool> paramsBool;

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
            stateHash = currentState.fullPathHash,
            normalizedTime = currentState.normalizedTime,
            paramsFloat = paramsFloat,
            paramsInt = paramsInt,
            paramsBool = paramsBool,
        };
    }

    public void Restore(Animator animator) {
        if (animator == null) return;

        animator.Play(stateHash, 0, normalizedTime);
        foreach (var param in paramsFloat) animator.SetFloat(param.Key, param.Value);
        foreach (var param in paramsInt) animator.SetInteger(param.Key, param.Value);
        foreach (var param in paramsBool) animator.SetBool(param.Key, param.Value);
    }
}