using System;
using HarmonyLib;
using NineSolsAPI;
using QFSW.QC;

namespace DebugMod.Modules;

[HarmonyPatch]
public class QuantumConsoleModule {
    // there's an exception that breaks the console
    // ReSharper disable once InconsistentNaming
    [HarmonyPatch(typeof(QuantumConsoleProcessor), "LoadCommandsFromType")]
    [HarmonyFinalizer]
    private static Exception LoadCommandsFromType(Type type, Exception __exception) => null;

    // ReSharper disable once InconsistentNaming
    [HarmonyPatch(typeof(QuantumConsole), "IsSupportedState")]
    [HarmonyPrefix]
    private static bool IsSupportedState(ref bool __result) {
        __result = true;
        return false;
    }

    public QuantumConsoleModule() {
        QuantumConsoleProcessor.GenerateCommandTable(true);
    }

    private bool active = false;

    public void ToggleConsole() {
        var consoleObject = ApplicationCore.Instance.gameObject.GetComponentInChildren<QuantumConsole>();
        consoleObject.enabled = true;

        try {
            if (active) {
                consoleObject.Deactivate();
                QuantumConsoleDeactivate();
            } else {
                consoleObject.Activate();
                QuantumConsoleActivate();
            }

            active = !active;
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    private void QuantumConsoleActivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.VoteForState(PlayerInputStateType.Console, DebugMod.Instance);
    }

    private void QuantumConsoleDeactivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.RevokeAllMyVote(DebugMod.Instance);
    }
}