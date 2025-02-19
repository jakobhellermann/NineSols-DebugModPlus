using HarmonyLib;
using NineSolsAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BepInEx.Configuration;
using NineSolsAPI.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameCore;
using Object = UnityEngine.Object;

namespace DebugModPlus.Modules;

/*TODO:
 * I wasn't really sure what the goals were of the segment system or ghost recording or how they worked, so I likely borked it. I tried not to but it might need some patching
 * My goal was to make this timer more closely imitate the standard use of a speedrun timer mod, which is a timer that mimics the autosplitter, used for timing short movement sections
 * based off collision triggers. In theory Livesplit should just be used for any longer section (until the ghost stuffs done)
 */

public enum TimerMode {
    // Default mode is triggers, the most common use for this timer is two triggers within the same room to time movement/bosses
    Triggers, // Savestates do not affect the timer
    AfterSavestate, // Timer starts immediately after savestate load
    NextRoom, // Timer starts next room after savestate load
}

internal enum SpeedrunTimerState {
    Inactive, // Off
    Running, // On, time increasing
    Paused, // On, manually paused
    Loading, // On, load remover following autosplitter logic https://github.com/buanjautista/LiveSplit-ASL/blob/main/NineSols-LoadRemover.asl
    StartNextRoom, // Off, turned on next room
}

internal class SpeedrunRecordingSegment {
    public required string SceneName;
    public float SegmentTime;
    public GhostFrame[]? GhostFrames;
}

internal class SpeedrunInfoText {
    public float SegmentTime;
    public float? SegmentTimeLast;
}

[HarmonyPatch]
public class SpeedrunTimerModule(ConfigEntry<TimerMode> configTimerMode) {
    private static bool isLoading = false;

    private const bool EnableGhost = false;

    private GhostModule GhostModule => DebugModPlus.Instance.GhostModule;
    private Stopwatch stopwatch = new();

    [HarmonyPatch(typeof(GameCore), "InitializeGameLevel")]
    [HarmonyPostfix]
    private static void InitializeGameLevel() {
        isLoading = false;
        var module = DebugModPlus.Instance.SpeedrunTimerModule;
        if (module.startRoom != GameCore.Instance.gameLevel.SceneName) module.OnLevelChangeDone();
        module.SpawnStartpointTexture();
        module.SpawnEndpointTexture();
    }

    [HarmonyPatch(typeof(GameCore), nameof(GameCore.ChangeScene), typeof(SceneConnectionPoint.ChangeSceneData),
        typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void ChangeScene() {
        isLoading = true;
        var module = DebugModPlus.Instance.SpeedrunTimerModule;
        module.OnLevelChange();
    }

    private GUIStyle? style;


    // state
    private float segmentStartTime = 0;

    private float currentTime = 0;

    // helps track loads and deltaTime
    private float latestTime = 0;

    private string? startRoom = null;

    private TimerMode TimerMode {
        get => configTimerMode.Value;
        set => configTimerMode.Value = value;
    }

    private SpeedrunTimerState state = SpeedrunTimerState.Inactive;

    // added startpoint
    private (Vector2, string)? startpoint = null;
    private (Vector2, string)? endpoint = null;
    private string[] timerModes = Enum.GetNames(typeof(TimerMode));


    private bool done = false;
    private List<SpeedrunRecordingSegment>? lastSegments = null;
    private List<SpeedrunRecordingSegment> currentSegments = new();
    private SpeedrunInfoText? infoText;

    private void OnLevelChange() {
        EndSegment();
    }

    private void OnLevelChangeDone() {
        SegmentBegin();

        if (state == SpeedrunTimerState.StartNextRoom && startRoom != GameCore.Instance.gameLevel.SceneName) {
            state = SpeedrunTimerState.Running;
            SegmentBegin();
        }
    }

    private void EndSegment() {
        if (state != SpeedrunTimerState.Running) return;

        var segmentTime = currentTime - segmentStartTime;
        segmentStartTime = currentTime;

        Log.Info($"Ending segment of {segmentTime:0.00}s");

        GhostFrame[]? ghostSegment = null;
        if (EnableGhost) {
            ghostSegment = GhostModule.CurrentRecording;
            GhostModule.StopRecording();
        }

        var lastSegment = lastSegments?.ElementAtOrDefault(currentSegments.Count);
        var currentSegment = new SpeedrunRecordingSegment {
            SceneName = GameCore.Instance.gameLevel.SceneName,
            SegmentTime = segmentTime,
            GhostFrames = ghostSegment,
        };
        currentSegments.Add(currentSegment);
        infoText = new SpeedrunInfoText {
            SegmentTime = currentSegment.SegmentTime,
            SegmentTimeLast = lastSegment != null ? currentSegment.SegmentTime - lastSegment.SegmentTime : null,
        };
    }

    private SpeedrunRecordingSegment? GetMatchingLastSegment() {
        if (lastSegments is null) return null;

        for (var i = 0;; i++) {
            if (i >= lastSegments.Count) return null;

            var lastSegment = lastSegments[i];

            if (i >= currentSegments.Count) return lastSegment;
        }
    }

    public void CycleTimerMode() {
        TimerMode = (TimerMode)((int)(TimerMode + 1) % timerModes.Length);
        ToastManager.Toast("Speedrun timer mode: " + TimerMode switch {
            TimerMode.Triggers => "Begin on start trigger",
            TimerMode.AfterSavestate => "Begin after savestate",
            TimerMode.NextRoom => "Begin on next room",
            _ => throw new ArgumentOutOfRangeException(),
        });

        ResetTimer();
        startRoom = null;
    }

    public void ResetTimer() {
        stopwatch.Reset();
        currentTime = 0;
        latestTime = 0;
        segmentStartTime = 0;
        done = false;
        currentSegments = new List<SpeedrunRecordingSegment>();
        infoText = null;
        state = SpeedrunTimerState.Inactive;
    }

    public void PauseTimer() {
        if (state == SpeedrunTimerState.Running) {
            ToastManager.Toast("Pausing timer");
            state = SpeedrunTimerState.Paused;
        } else {
            ToastManager.Toast("Resuming timer");
            state = SpeedrunTimerState.Running;
        }
    }

    public void SetStartpoint() {
        var startpointPosition = Player.i.transform.position;
        //Adjust position to match outer edges
        startpointPosition.x += Player.i.Facing == Facings.Right ? 16 : -16;
        var startpointScene = GameCore.Instance.gameLevel.SceneName;
        startpoint = (startpointPosition, startpointScene);
        SpawnStartpointTexture();
    }

    public void SetEndpoint() {
        var endpointPosition = Player.i.transform.position;
        endpointPosition.x += Player.i.Facing == Facings.Right ? 16 : -16;
        var endpointScene = GameCore.Instance.gameLevel.SceneName;
        endpoint = (endpointPosition, endpointScene);
        SpawnEndpointTexture();
    }

    public void ClearCheckpoints() {
        startpoint = null;
        endpoint = null;
        if (startpointObject) Object.Destroy(startpointObject);
        if (endpointObject) Object.Destroy(endpointObject);
    }

    private void SegmentBegin() {
        segmentStartTime = currentTime;
        if (EnableGhost) GhostModule.StartRecording();

        if (EnableGhost) {
            var matchingSegment = GetMatchingLastSegment();
            if (matchingSegment.GhostFrames is not null) {
                ToastManager.Toast($"playing back {matchingSegment.GhostFrames.Length} frames");
                GhostModule.Playback(matchingSegment.GhostFrames);
            }
        }
    }

    public void OnSavestateCreated() {
        // dont reset timer if trigger mode
        if (TimerMode == TimerMode.Triggers) return;
        done = false;
        currentSegments = new List<SpeedrunRecordingSegment>();
        lastSegments = null;
        infoText = null;
        segmentStartTime = 0;
        startRoom = null;
    }

    public void OnSavestateLoaded() {
        // dont reset timer if trigger mode
        if (TimerMode == TimerMode.Triggers) return;
        ResetTimer();
        startRoom ??= GameCore.Instance.gameLevel.SceneName;
        state = TimerMode switch {
            TimerMode.AfterSavestate => SpeedrunTimerState.Running,
            TimerMode.NextRoom => SpeedrunTimerState.StartNextRoom,
            _ => throw new ArgumentOutOfRangeException(),
        };

        if (state == SpeedrunTimerState.Running) SegmentBegin();
    }

    private Sprite? checkpointSprite;
    private GameObject? startpointObject;
    private GameObject? endpointObject;

    private Sprite GetCheckpointSprite() {
        var checkpointTexture = AssemblyUtils.GetEmbeddedTexture("DebugModPlus.checkmark.png")!;
        return checkpointSprite ??= Sprite.CreateSprite(
            checkpointTexture, new Rect(0, 0, checkpointTexture.width, checkpointTexture.height), new Vector2(0.5f, 0f),
            16f,
            0,
            SpriteMeshType.FullRect, Vector4.zero, false, new SecondarySpriteTexture[] { }
        );
    }


    private void SpawnEndpointTexture() {
        if (endpoint is not var (position, scene)) return;

        if (GameCore.Instance.gameLevel.SceneName != scene) return;

        if (endpointObject) Object.Destroy(endpointObject);
        endpointObject = new GameObject("flag") {
            transform = { position = position },
        };
        var spriteRenderer = endpointObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetCheckpointSprite();
        spriteRenderer.material.shader = Shader.Find("GUI/Text Shader");
        spriteRenderer.material.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
    }

    // Startpoint logic can probably be merged with Endpoint for better readability
    private void SpawnStartpointTexture() {
        if (startpoint is not var (position, scene)) return;

        if (GameCore.Instance.gameLevel.SceneName != scene) return;

        if (startpointObject) Object.Destroy(startpointObject);
        startpointObject = new GameObject("flag") {
            transform = { position = position },
        };
        var spriteRenderer = startpointObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetCheckpointSprite();
        spriteRenderer.material.shader = Shader.Find("GUI/Text Shader");
        spriteRenderer.material.color = new Color(0.0f, 0.6f, 0.3f, 0.8f);
    }

    private void StartpointReached() {
        if (state == SpeedrunTimerState.Running || state == SpeedrunTimerState.Loading) return;

        ResetTimer();

        state = SpeedrunTimerState.Running;
        SegmentBegin();
    }

    private void EndpointReached() {
        if (state != SpeedrunTimerState.Running || state == SpeedrunTimerState.Loading) return;

        EndSegment();
        done = true;
        state = SpeedrunTimerState.Inactive;
        lastSegments = currentSegments;
        ToastManager.Toast($"Endpoint reached in {currentSegments.Count} segments");
        currentSegments = new List<SpeedrunRecordingSegment>();
    }

    /* This is where a huge disaster occurred
     * In order to follow autosplitter logic theres a lot of data that needs to be grabbed, however referencing manager instances that aren't active
     * crashes the game, hopefully I made this readable
     */
    public void LateUpdate() {
        try {
            var coreState = GameCoreState.Playing;
            var sceneName = SceneManager.GetActiveScene().name;
            UnityEngine.UI.Image? blackCover = null;
            LoadingScreenPanel? loadingScreen = null;

            void GrabInfoSafe() {
                // want to grab these references in safe spots
                if (sceneName == "ClearTransition") {
                    loadingScreen = null;
                    blackCover = null;
                } else if (sceneName == "TitleScreenMenu") {
                    loadingScreen = null;
                    if (!blackCover) blackCover = ApplicationUIGroupManager.Instance.blackCover;
                } else {
                    blackCover = null;
                    if (!loadingScreen) loadingScreen = ApplicationCore.Instance.loadingScreen;
                    coreState = GameCore.Instance.currentCoreState;
                }
            }

            // check timer triggers, start trigger might happen when inactive
            CheckTriggers(sceneName);

            if (state == SpeedrunTimerState.Inactive)
                return;
            else if (state == SpeedrunTimerState.Paused) {
                stopwatch.Stop();
                latestTime = (float)stopwatch.Elapsed.TotalSeconds;
                return;
            }

            GrabInfoSafe();
            CheckLoading(sceneName, coreState, blackCover, loadingScreen);

            if (state == SpeedrunTimerState.Running) {
                // Stopwatch is preferred over deltatime so it's unaffected by bow and other things, more accurate to livesplit
                stopwatch.Start();
                currentTime += (float)stopwatch.Elapsed.TotalSeconds - latestTime;
                latestTime = (float)stopwatch.Elapsed.TotalSeconds;
            }

            // don't pause stopwatch during loads to track load removal
        } catch (Exception e) {
            Log.Error($"Error during SpeedrunTimerModule LateUpdate: {e}");
        }
    }

    // All logic here is the autosplitter's fault not mine
    private void CheckLoading(
        string sceneName = "",
        GameCoreState coreState = GameCoreState.Playing,
        UnityEngine.UI.Image? blackCover = null,
        LoadingScreenPanel? loadingScreen = null) {
        if (state is not (SpeedrunTimerState.Running or SpeedrunTimerState.Loading)) return;

        try {
            var doPauseTimer = (loadingScreen is not null && loadingScreen.isActiveAndEnabled)
                               // Autosplitter documents this as Blank / Load, not sure if this is how I'm supposed to check it
                               || sceneName is "" or "ClearTransition" or "A0_S6_Intro_Video"
                               // Save file reads synchronous so this is the only way to solve that lag
                               || (sceneName == "TitleScreenMenu" && blackCover is
                                   { isActiveAndEnabled: true, color.a: > 0.99f })
                               || coreState is GameCoreState.ChangingScene or GameCoreState.Init;

            if (doPauseTimer) {
                if (state == SpeedrunTimerState.Running) {
                    Log.Info("Pausing timer now");
                    state = SpeedrunTimerState.Loading;
                }
            } else {
                if (state == SpeedrunTimerState.Loading) {
                    var oldLatestTime = latestTime;
                    latestTime = (float)stopwatch.Elapsed.TotalSeconds;
                    Log.Info("Unpausing timer, load time removed is " + (latestTime - oldLatestTime));

                    state = SpeedrunTimerState.Running;
                }
            }
        } catch (Exception e) {
            Log.Error($"Error during loading checks in SpeedrunTimerModule CheckLoading: {e}");
        }
    }

    private void CheckTriggers(string sceneName = "") {
        var playerPosition = Vector2.zero;
        var doCheckPoints = false;
        const float distanceThreshold = 18;

        if (Player.i && Player.i.gameObject.activeInHierarchy && Player.i.transform) {
            playerPosition = Player.i.transform.position;
            doCheckPoints = true;
        }

        if (doCheckPoints && startpoint is var (sposition, sscene) && sscene == sceneName) {
            var sdistance = Vector2.Distance(playerPosition, sposition);
            if (sdistance < distanceThreshold) StartpointReached();
        }

        if (doCheckPoints && endpoint is var (eposition, escene) && escene == sceneName) {
            var edistance = Vector2.Distance(playerPosition, eposition);
            if (edistance < distanceThreshold) EndpointReached();
        }
    }

    //TODO: Track Segment PB as well to compare
    public void OnGui() {
        style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 30 };
        const int padding = 8;

        if (done || state != SpeedrunTimerState.Inactive) {
            var timeStr = $"{(done ? "Done in " : "")}{currentTime:0.00}s";

            if (infoText != null) {
                var diffText = $"{(infoText.SegmentTimeLast > 0 ? "+" : "")}{infoText.SegmentTimeLast:0.00}s";
                timeStr += $"\nCompared to last: {diffText}";
            }

            GUI.Label(new Rect(padding, padding, 600, 100), timeStr, style);
        }
    }

    public void Destroy() {
        if (startpointObject) Object.Destroy(startpointObject);
        if (endpointObject) Object.Destroy(endpointObject);
    }
}