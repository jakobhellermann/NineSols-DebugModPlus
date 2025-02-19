using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus.Modules;

public record GhostFrame(Vector3 Position, string SpriteName, int Facing);

internal class GhostPlayback(SpriteRenderer playerCopy, GhostFrame[] frames, int playbackIndex) {
    public SpriteRenderer PlayerCopy = playerCopy;
    public GhostFrame[] Frames = frames;
    public int PlaybackIndex = playbackIndex;
}

public class GhostModule {
    // sprite cache
    private Dictionary<string, Sprite> playerSprites = new();

    // recording
    private bool recording = false;
    private List<GhostFrame> recordingFrames = new();

    public GhostFrame[] CurrentRecording => recordingFrames.ToArray();

    // playback
    private List<GhostPlayback> playbacks = new();

    public void StartRecording() {
        recordingFrames.Clear();
        recording = true;
    }

    public void StopRecording() {
        recording = false;
    }

    public void ToggleRecording() {
        if (recording) StopRecording();
        else StartRecording();
    }

    public void Playback(GhostFrame[] frames) {
        var player = Player.i;
        var playerCopy = Object.Instantiate(player.PlayerSprite.gameObject).GetComponent<SpriteRenderer>();
        Object.DontDestroyOnLoad(playerCopy);

        playbacks.Add(new GhostPlayback(playerCopy, frames, 0));
    }

    private void UpdateRecord() {
        var player = Player.i;
        if (!player) return; // TODO?

        recordingFrames.Add(
            new GhostFrame(player.transform.position, player.PlayerSprite.sprite.name, (int)player.Facing));
    }

    private void UpdatePlayback(GhostPlayback playback, GhostFrame ghostFrame) {
        playback.PlayerCopy.transform.position = ghostFrame.Position + Vector3.down * 3.5f;
        playerSprites.TryGetValue(ghostFrame.SpriteName, out var sprite);
        playback.PlayerCopy.sprite = sprite;
        playback.PlayerCopy.transform.localScale = new Vector3(ghostFrame.Facing, 1f, 1f);
    }

    public void LateUpdate() {
        if (recording) UpdateRecord();

        for (var i = playbacks.Count - 1; i >= 0; i--) {
            var playback = playbacks[i];

            if (playback.PlaybackIndex < playback.Frames.Length) {
                UpdatePlayback(playback, playback.Frames[playback.PlaybackIndex]);
                playback.PlaybackIndex++;
            } else {
                Object.Destroy(playback.PlayerCopy);
                playbacks.RemoveAt(i);
            }
        }

        if (Player.i is { } player) {
            var sprite = player.PlayerSprite.sprite;
            var name = sprite.name;
            playerSprites.TryAdd(name, sprite);
        }
    }

    public void Unload() {
        playbacks.ForEach(playback => Object.Destroy(playback.PlayerCopy));
        playbacks.Clear();
    }
}