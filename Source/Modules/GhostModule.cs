using System.Collections.Generic;
using DebugModPlus;
using System.Security.Cryptography;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UIElements;

namespace DebugModPlus.Modules;

public record GhostFrame(Vector3 Position, string SpriteName, int Facing, double Timestamp);

internal class GhostPlayback(SpriteRenderer playerCopy, GhostFrame[] frames, int playbackIndex) {
    public SpriteRenderer PlayerCopy = playerCopy;
    public GhostFrame[] Frames = frames;
    public int PlaybackIndex = playbackIndex;
}

public class GhostModule(ConfigEntry<Color> ghostColor) {
    private SpeedrunTimerModule SpeedrunTimerModule = DebugModPlus.Instance.SpeedrunTimerModule;
    // sprite cache
    private Dictionary<string, Sprite> playerSprites = new();

    // recording
    private bool recording = false;
    private List<GhostFrame> recordingFrames = new();

    public GhostFrame[] CurrentRecording => recordingFrames.ToArray();

    // playback
    private List<GhostPlayback> playbacks = new();

    //flag
    private bool doClearGhosts = false;
    private bool pauseStopsTimer = false;

    public void StartRecording(bool _pauseStopsTimer = false) {
        recordingFrames.Clear();
        recording = true;
        pauseStopsTimer = _pauseStopsTimer;
    }

    public void StopRecording() {
        recording = false;
    }

    public void ToggleRecording() {
        if (recording) StopRecording();
        else StartRecording();
    }

    public void ClearGhosts()
    {
        for (var i = playbacks.Count - 1; i >= 0; i--)
        {
            var playback = playbacks[i];
            GhostDestroy(playback, i);
        }
    }

    private void GhostDestroy(GhostPlayback playback, int _playbacksIndex)
    {
        Object.Destroy(playback.PlayerCopy);
        playbacks.RemoveAt(_playbacksIndex);
    }

    public void Playback(GhostFrame[] frames) {
        var player = Player.i;
        var playerCopy = Object.Instantiate(player.PlayerSprite.gameObject).GetComponent<SpriteRenderer>();
        Object.DontDestroyOnLoad(playerCopy);

        playerCopy.color = ghostColor.Value;

        playbacks.Add(new GhostPlayback(playerCopy, frames, 0));
    }

    private void UpdateRecord() {
        var player = Player.i;
        //we want to use current time since loads can interrupt the stopwatch pause, but if we need to we'll use stopwatch
        var time = SpeedrunTimerModule.currentTime;
        if (time == 0) time = (float)SpeedrunTimerModule.stopwatch.Elapsed.TotalSeconds;
        if (!player) return; // TODO?
        if (pauseStopsTimer && RCGTime.timeScale == 0) return;
        
        recordingFrames.Add(
            new GhostFrame(player.transform.position, player.PlayerSprite.sprite.name, (int)player.Facing, time));
    }

    private void CheckPlayback(GhostPlayback playback, bool _clearingGhosts = false, int _playbacksIndex = 0)
    {
        int playbacksIndex = _playbacksIndex;
        var time = SpeedrunTimerModule.currentTime;
        var stopwatchTime = (float)SpeedrunTimerModule.stopwatch.Elapsed.TotalSeconds;
        if (time == 0 || SpeedrunTimerModule.state == SpeedrunTimerState.InactiveDone
                      || SpeedrunTimerModule.state == SpeedrunTimerState.Inactive)
            time = stopwatchTime;

        try
        {
            if (playback.PlaybackIndex < playback.Frames.Length)
            {
                GhostFrame ghostFrame = playback.Frames[playback.PlaybackIndex];
                //check if games paused
                if (pauseStopsTimer && RCGTime.timeScale == 0) return;
                else if (ghostFrame.Timestamp < time)
                {
                    UpdatePlayback(playback, ghostFrame, playbacksIndex);
                }
                else if (time == 0)
                {
                    GhostDestroy(playback, playbacksIndex);
                }
            }
            else
            {
                GhostDestroy(playback, playbacksIndex);
            }
        }
        catch (System.Exception e)
        {
            Log.Error("Playback Index: " + playback.PlaybackIndex + "Playback Frame Length: " + playback.Frames.Length);
            Log.Error(e);
        }
    }

    private void UpdatePlayback(GhostPlayback playback, GhostFrame ghostFrame, int _playbacksIndex) {

        int playbacksIndex = _playbacksIndex;
        playback.PlayerCopy.transform.position = ghostFrame.Position + Vector3.down * 3.5f;
        playerSprites.TryGetValue(ghostFrame.SpriteName, out var sprite);
        playback.PlayerCopy.sprite = sprite;
        playback.PlayerCopy.transform.localScale = new Vector3(ghostFrame.Facing, 1f, 1f);
        //Increment index when the frame actually plays
        playback.PlaybackIndex++;
        //Loop this until its caught up to the current time
        CheckPlayback(playback, false, playbacksIndex);
    }

    public void LateUpdate() {
        bool clearingGhosts = doClearGhosts;
        doClearGhosts = false;

        if (recording) UpdateRecord();

        for (var i = playbacks.Count - 1; i >= 0; i--) {
            var playback = playbacks[i];

            CheckPlayback(playback, clearingGhosts, i);
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