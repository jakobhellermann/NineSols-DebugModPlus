using HarmonyLib;

namespace DebugMod.Modules;

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
}