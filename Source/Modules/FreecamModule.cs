using UnityEngine;

namespace DebugMod.Source.Modules;

public class FreecamModule {
    private const float Speed = 200;
    private const float FastMultiplier = 3;
    private const bool CenterCam = true;

    private static bool freecamActive;

    private static PlayerInputStateType stateBefore = PlayerInputStateType.Cutscene;


    [BindableMethod(Name = "Toggle Freecam")]
    private static void ToggleFreecam() {
        freecamActive = !freecamActive;

        var player = Player.i;
        var playerInput = Player.i.playerInput;

        if (freecamActive) {
            player.enabled = false;
            player.health.BecomeInvincible(Plugin.Instance);
            // stateBefore = playerInput.fsm.State;
            // playerInput.fsm.ChangeState(PlayerInputStateType.Console);
            // Player.i.DisableGravity();
        } else {
            player.enabled = true;
            player.health.RemoveInvincible(Plugin.Instance);
            // playerInput.fsm.ChangeState(stateBefore);
            // Player.i.EnableGravity();
        }
    }

    public static void Update() {
        if (!freecamActive) return;
        var goFast = Input.GetKey(KeyCode.LeftShift);
        var freecamSpeed = Speed * (goFast ? FastMultiplier : 1);

        var input = new Vector2(
            Input.GetAxis("Horizontal"),
            Input.GetAxis("Vertical"));

        Player.i.SetPosition(Player.i.transform.position + (Vector3)(input * (Time.deltaTime * freecamSpeed)));
        if (CenterCam) CameraManager.Instance.camera2D.MoveCameraInstantlyToPosition(Player.i.transform.position);
    }
}