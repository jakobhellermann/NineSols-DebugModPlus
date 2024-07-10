using System.Collections.Generic;
using BepInEx.Configuration;
using NineSolsAPI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugMod.Modules;

internal class GhostFrame(Vector3 position, string spriteName, int facing) {
    public Vector3 Position = position;
    public string SpriteName = spriteName;
    public int Facing = facing;
};

public class GhostModule {
    private SpriteRenderer playerCopy;

    // recording/playback
    private bool recording = false;
    private List<GhostFrame> recordingFrames = [];
    private int? playbackIndex = null;

    // sprite cache
    private Dictionary<string, Sprite> playerSprites = new();

    public void ToggleRecording() {
        recording = !recording;
        if (recording) recordingFrames.Clear();
        ToastManager.Toast($"Recording: {recording}");
    }

    public void PlayBack() {
        playbackIndex = 0;

        ToastManager.Toast($"Playing back {recordingFrames.Count} frames");

        if (playerCopy) Object.Destroy(playerCopy);
        var player = Player.i;
        playerCopy = Object.Instantiate(player.PlayerSprite.gameObject).GetComponent<SpriteRenderer>();
    }

    /*public void Test() {
        if (playerCopy) Object.Destroy(playerCopy);
        var recorder = Player.i.playerInput.gameObject.GetComponent<PlayerInputRecorder>();
        recorder.StartNextRecording();
        var player = Player.i;
        ToastManager.Toast(player.gameObject.scene);
        playerCopy = Object.Instantiate(player.PlayerSprite.gameObject);
    }*/

    private void UpdateRecord() {
        var player = Player.i;

        recordingFrames.Add(
            new GhostFrame(player.transform.position, player.PlayerSprite.sprite.name, (int)player.Facing));
    }

    private void UpdatePlayback(GhostFrame ghostFrame) {
        playerCopy.transform.position = ghostFrame.Position + Vector3.down * 3.5f;
        playerSprites.TryGetValue(ghostFrame.SpriteName, out var sprite);
        playerCopy.sprite = sprite;
        playerCopy.transform.localScale = new Vector3(ghostFrame.Facing, 1f, 1f);
    }

    public void LateUpdate() {
        if (recording) UpdateRecord();
        if (playbackIndex is { } idx) {
            if (idx >= recordingFrames.Count) {
                Object.Destroy(playerCopy);
                playbackIndex = null;
            } else {
                UpdatePlayback(recordingFrames[idx]);
                playbackIndex++;
            }
        }

        if (Player.i is { } player) {
            var sprite = player.PlayerSprite.sprite;
            var name = sprite.name;
            playerSprites.TryAdd(name, sprite);
        }

        /*foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
            if (Input.GetKey(kcode))
                ToastManager.Toast("KeyCode down: " + kcode);*/
    }

    public void Unload() {
        if (playerCopy) Object.Destroy(playerCopy);
    }
}