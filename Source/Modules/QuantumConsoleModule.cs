using System;
using HarmonyLib;
using QFSW.QC;
using UnityEngine.SceneManagement;

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
    
    
    private bool consoleInitialized;

    public QuantumConsoleModule() {
        QuantumConsoleProcessor.GenerateCommandTable(true);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void Unload() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
        if (consoleInitialized || !QuantumConsole.Instance) return;
        
        QuantumConsole.Instance.OnActivate += QuantumConsoleActivate;
        QuantumConsole.Instance.OnDeactivate += QuantumConsoleDeactivate;
        consoleInitialized = true;
    }

    private void QuantumConsoleActivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.VoteForState(PlayerInputStateType.Console, QuantumConsole.Instance);
    }

    private void QuantumConsoleDeactivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.RevokeAllMyVote(QuantumConsole.Instance);
    }

}