using Com.LuisPedroFonseca.ProCamera2D;
using HarmonyLib;
using UnityEngine;

namespace DebugMod.Modules;

[HarmonyPatch]
public class FreecamModule {
    private const float Speed = 200;
    private const float FastMultiplier = 3;
    private const float ScrollSpeed = 1;

    public static bool FreecamActive;

    private static PlayerInputStateType stateBefore = PlayerInputStateType.Cutscene;

    private static Camera sceneCamera => GameCore.Instance.gameLevel.sceneCamera;
    private static ProCamera2D proCamera => CameraManager.Instance.cameraCore.proCamera2D;

    [BindableMethod(Name = "Toggle Freecam", DefaultKeybind = [KeyCode.LeftControl, KeyCode.M])]
    private static void ToggleFreecam() {
        FreecamActive = !FreecamActive;

        var player = Player.i;
        var playerInput = Player.i.playerInput;

        if (FreecamActive) {
            player.enabled = false;
            player.health.BecomeInvincible(DebugMod.Instance);
            proCamera.enabled = false;


            // CameraManager.Instance.cameraCore.theRealSceneCamera.enabled = false;
            // stateBefore = playerInput.fsm.State;
            // playerInput.fsm.ChangeState(PlayerInputStateType.Console);
            // Player.i.DisableGravity();
        } else {
            // CameraManager.Instance.cameraCore.theRealSceneCamera.enabled = true;

            player.enabled = true;
            player.health.RemoveInvincible(DebugMod.Instance);
            proCamera.enabled = true;
            sceneCamera.transform.position = sceneCamera.transform.position with { z = -240 }; // TODO check if correct
            // playerInput.fsm.ChangeState(stateBefore);
            // Player.i.EnableGravity();
        }
    }

    public static void Update() {
        if (!FreecamActive) return;
        var goFast = Input.GetKey(KeyCode.LeftShift);
        var freecamSpeed = Speed * (goFast ? FastMultiplier : 1);

        var mouseScroll = Input.mouseScrollDelta.y;
        if (mouseScroll != 0) {
            var sceneCamTransform = sceneCamera.transform;

            var diff = mouseScroll < 0 ? 1.1f : 0.9f;
            sceneCamTransform.position =
                sceneCamTransform.position with { z = sceneCamTransform.position.z * diff * ScrollSpeed };
        }

        var x = (Input.GetKey(KeyCode.D) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) ? 1 : 0);
        var y = (Input.GetKey(KeyCode.W) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) ? 1 : 0);
        var input = new Vector2(x, y);


        Player.i.SetPosition(Player.i.transform.position + (Vector3)(input * (Time.deltaTime * freecamSpeed)));
        CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);

        // var cam = CameraManager.Instance.camera2D;
        // ToastManager.Toast(cam.transform.localPosition);
    }
}