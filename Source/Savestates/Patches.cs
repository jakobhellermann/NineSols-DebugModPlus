using System.Reflection;
using DebugModPlus.Interop;
using HarmonyLib;

namespace DebugModPlus.Savestates;

// ReSharper disable once InconsistentNaming
[HarmonyPatch]
public class Patches {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Actor), nameof(Actor.OnRebindAnimatorMove))]
    [HarmonyPatch(typeof(Player), nameof(Player.OnRebindAnimatorMove))]
    [HarmonyPatch(typeof(MonsterBase), nameof(MonsterBase.OnRebindAnimatorMove))]
    public static bool PreventDuringLoad(MethodBase __originalMethod) => !DebugModPlusInterop.IsLoadingSavestate;
}