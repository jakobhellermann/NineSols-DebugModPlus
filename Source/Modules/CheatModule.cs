using HarmonyLib;

namespace DebugModPlus.Modules;

[HarmonyPatch]
public class CheatModule {
    [BindableMethod(Name = "Refill all")]
    private static void RefillAll() {
        if (Player.i is { } player) {
            player.health.GainFull();
            player.ammo.GainFull();
            player.chiContainer.GainFull();
        }
    }

    [BindableMethod(Name = "Unlock all maps")]
    private static void UnlockMaps() {
        foreach (var areaCollection in GameCore.Instance.allAreas.allCollections)
        foreach (var data in areaCollection.levelMapDatas) {
            data.Unlocked.CurrentValue = true;
            foreach (var x in data.MistMapDataEntries) x.BindingFlag.CurrentValue = true;
        }
    }

    [BindableMethod(Name = "Give Ledge Storage")]
    private static void GiveLedgeStorage() {
        if (Player.i is not { } player) return;

        player.isOnLedge = true;
    }
}