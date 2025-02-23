using HarmonyLib;
using NineSolsAPI;
using UnityEngine.Events;

namespace DebugModPlus;

public static class PatchesSpeedrunPatch {
    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeToBlack))]
    [HarmonyPrefix]
    private static void FadeToBlack(ref float fadeTime) {
        if (FastLoads.Enabled) fadeTime = 0;
    }

    [HarmonyPatch(typeof(GameCore),
        nameof(GameCore.ChangeScene),
        typeof(SceneConnectionPoint.ChangeSceneData),
        typeof(bool),
        typeof(bool))]
    [HarmonyPrefix]
    private static void ChangeScene(
        ref SceneConnectionPoint.ChangeSceneData changeSceneData,
        ref bool showTip,
        bool captureLastImage
    ) {
        DebugModPlus.Instance.SpeedrunTimerModule.OnLevelChange();

        if (FastLoads.Enabled) {
            showTip = false;
        }
    }
}

public static class PatchesCurrentPatch {
    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeToBlack), typeof(float), typeof(float))]
    [HarmonyPatch(typeof(GameCore), nameof(GameCore.FadeToBlack), typeof(float), typeof(UnityAction), typeof(float))]
    [HarmonyPrefix]
    private static void FadeToBlack(ref float fadeTime) {
        if (FastLoads.Enabled) fadeTime = 0;
    }

    [HarmonyPatch(typeof(GameCore),
        nameof(GameCore.ChangeScene),
        typeof(SceneConnectionPoint.ChangeSceneData),
        typeof(bool),
        typeof(bool),
        typeof(float))]
    [HarmonyPrefix]
    private static void ChangeScene(
        ref SceneConnectionPoint.ChangeSceneData changeSceneData,
        ref bool showTip,
        bool captureLastImage,
        ref float delayTime
    ) {
        DebugModPlus.Instance.SpeedrunTimerModule.OnLevelChange();

        if (FastLoads.Enabled) {
            delayTime = 0;
            showTip = false;
        }
    }
}