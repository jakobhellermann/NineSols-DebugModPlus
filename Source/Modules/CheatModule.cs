using HarmonyLib;
using JetBrains.Annotations;
using QFSW.QC;
using UnityEngine;

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
    [Command("debugmodplus.cheats.nss")]
    private static void CmdNymphStateStorage() {
        if (Player.i is not { } player) return;

        var playerNymphState =
            (PlayerHackDroneControlState)player.fsm.FindMappingState(PlayerStateType.HackDroneControl);
        var nymph = playerNymphState.hackDrone;
        nymph.gameObject.SetActive(true);
        playerNymphState.OnStateEnter();
        playerNymphState.OnStateExit();
        nymph.ChangeState(HackDrone.DroneStateType.Normal);
    }

    [UsedImplicitly]
    [Command("debugmodplus.cheats.nss.outofrange")]
    private static void CmdNymphStateStorageFinished() {
        if (Player.i is not { } player) return;

        var playerNymphState =
            (PlayerHackDroneControlState)player.fsm.FindMappingState(PlayerStateType.HackDroneControl);
        var nymph = playerNymphState.hackDrone;
        nymph.gameObject.SetActive(true);
        playerNymphState.OnStateEnter();
        playerNymphState.OnStateExit();
        nymph.ChangeState(HackDrone.DroneStateType.Normal);

        nymph.transform.position = Vector3.zero;
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
            if (!data) continue;

            data.Unlocked.CurrentValue = true;
            foreach (var x in data.MistMapDataEntries) x.BindingFlag.CurrentValue = true;
        }
    }
}