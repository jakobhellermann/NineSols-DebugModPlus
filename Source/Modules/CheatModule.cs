using HarmonyLib;
using JetBrains.Annotations;
using NineSolsAPI;
using QFSW.QC;

// ReSharper disable ExplicitCallerInfoArgument

namespace DebugModPlus.Modules;

[HarmonyPatch]
public class CheatModule {
    [UsedImplicitly]
    [Command("debugmodplus.cheats.ledge_storage")]
    private static void CmdLedgeStorage() {
        if (Player.i is not { } player) return;

        player.isOnLedge = true;
    }

    [UsedImplicitly]
    [Command("debugmodplus.cheats.refill")]
    private static void CmdHeal() {
        RefillAll();
    }


    [BindableMethod(Name = "Refill all")]
    private static void RefillAll() {
        if (Player.i is not { } player) return;

        player.health.GainFull();
        player.ammo.GainFull();
        player.chiContainer.GainFull();
    }

    [BindableMethod(Name = "Unlock all maps")]
    private static void UnlockMaps() {
        foreach (var areaCollection in GameCore.Instance.allAreas.allCollections)
        foreach (var data in areaCollection.levelMapDatas) {
            data.Unlocked.CurrentValue = true;
            foreach (var x in data.MistMapDataEntries) x.BindingFlag.CurrentValue = true;
        }
    }
}