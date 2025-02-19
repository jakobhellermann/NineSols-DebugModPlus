using System;
using DebugMod.Modules;
using HarmonyLib;
using NineSolsAPI;
using UnityEngine;

namespace DebugMod;

[HarmonyPatch]
public class FastLoads {
    private static bool DoFastLoads => FreecamModule.FreecamActive || SavestateModule.IsLoadingSavestate;

    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeToBlack))]
    [HarmonyPrefix]
    private static void FadeToBlack(ref float fadeTime) {
        if (DoFastLoads) fadeTime = 0;
    }


    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeOutBlack))]
    [HarmonyPrefix]
    private static void FadeOutBlack(ref float fadeTime, ref float delayTime) {
        if (!DoFastLoads) return;

        fadeTime = 0;
        delayTime = 0;
    }

    [HarmonyPatch(typeof(UIExtension), "AddUITask")]
    [HarmonyPrefix]
    private static void AddUiTask(MonoBehaviour mb, Action action, ref float delay) {
        if (!DoFastLoads) return;

        delay = 0;
    }

    //check this
    [HarmonyPatch(typeof(GameCore), nameof(GameCore.ChangeScene),
    new Type[] { typeof(SceneConnectionPoint.ChangeSceneData), typeof(bool), typeof(bool) })]
    [HarmonyPrefix]
    private static void ChangeScene(
        ref SceneConnectionPoint.ChangeSceneData changeSceneData,
        ref bool showTip,
        bool captureLastImage
    ) {
        if (!DoFastLoads) return;

        showTip = false;
    }

    /*[HarmonyPatch(typeof(SceneConnectionPoint.ChangeSceneData),
        nameof(SceneConnectionPoint.ChangeSceneData.MarkAsLoaded))]
    [HarmonyPostfix]
    private static void MarkAsLoaded(ref SceneConnectionPoint.ChangeSceneData __instance) {
        // ToastManager.Toast($"Scene load took {__instance.loadingTime}");
    }*/
}