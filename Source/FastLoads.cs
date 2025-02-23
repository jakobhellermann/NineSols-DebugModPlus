using System;
using DebugModPlus.Modules;
using HarmonyLib;
using UnityEngine;

namespace DebugModPlus;

[HarmonyPatch]
public class FastLoads {
    public static bool Enabled => FreecamModule.FreecamActive || SavestateModule.IsLoadingSavestate;

    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeOutBlack))]
    [HarmonyPrefix]
    private static void FadeOutBlack(ref float fadeTime, ref float delayTime) {
        if (!Enabled) return;

        fadeTime = 0;
        delayTime = 0;
    }

    [HarmonyPatch(typeof(UIExtension), "AddUITask")]
    [HarmonyPrefix]
    private static void AddUiTask(MonoBehaviour mb, Action action, ref float delay) {
        if (!Enabled) return;

        delay = 0;
    }
}