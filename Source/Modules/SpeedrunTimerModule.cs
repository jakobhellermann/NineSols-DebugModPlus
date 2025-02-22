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
    StartNextRoom, // Off, turned on next room
    Running, // On, time increasing
    Paused, // On, manually paused
    Loading, // On, load remover following autosplitter logic https://github.com/buanjautista/LiveSplit-ASL/blob/main/NineSols-LoadRemover.asl
    InactiveDone, // Off, after reaching the end
}

// ReSharper disable once InconsistentNaming
internal class Segments {
    public List<SpeedrunRecordingSegment> segments = new();
    public float? FinishedTime;

    public void Clear() {
        segments.Clear();
        FinishedTime = null;
    }

    public void RecalcFinishedTime() {
        FinishedTime = segments.Sum(segment => segment.SegmentTime);
    }

    public Segments Copy() => new() {
        segments = new List<SpeedrunRecordingSegment>(segments),
        FinishedTime = FinishedTime,
    };
}

internal class SegmentHistory {
    public Segments Current = new();
    public Segments Last = new();
    public Segments PB = new();
    // ^ segments are SOB, FinishedTime is whole

    public float? FinishedPbDelta = null;

    public void Clear() {
        Current.Clear();
        ClearOld();
    }

    public void ClearOld() {
        Last.Clear();
        PB.Clear();
        FinishedPbDelta = null;
    }

    public void Add(SpeedrunRecordingSegment currentSegment) {
        Current.segments.Add(currentSegment);
    }

    public void Finish() {
        var previousLast = Last;
        previousLast.Clear();

        Last = Current;
        Last.RecalcFinishedTime();
        Current = previousLast;

        if (Last.segments.Count != PB.segments.Count && PB.segments.Count > 0)
            ToastManager.Toast(
                $"Finished in {Last.segments.Count} segments, as opposed to PB of {PB.segments.Count} segments. Clearing PB.");

        FinishedPbDelta = Last.FinishedTime - PB.FinishedTime;

        if (PB.segments.Count == 0) {
            PB = Last.Copy();
        } else {
            if (Last.FinishedTime < PB.FinishedTime) {
                PB.FinishedTime = Last.FinishedTime;
            }

            for (var i = 0; i < PB.segments.Count; i++) {
                if (Last.segments[i].SegmentTime < PB.segments[i].SegmentTime) {
                    PB.segments[i] = Last.segments[i];
                }
            }
        }
    }


    public int ActiveSegmentIndex => Current.segments.Count;
}

internal class SpeedrunRecordingSegment {
    public required string SceneName;
    public float SegmentTime;
    public GhostFrame[]? GhostFrames;
}

[HarmonyPatch]
public class SpeedrunTimerModule(
    ConfigEntry<TimerMode> configTimerMode,
    ConfigEntry<bool> configRecordGhost
) {
    private GhostModule GhostModule => DebugModPlus.Instance.GhostModule;
    private Stopwatch stopwatch = new();

    [HarmonyPatch(typeof(GameCore), "InitializeGameLevel")]
    [HarmonyPostfix]
    private static void InitializeGameLevel() {
        var module = DebugModPlus.Instance.SpeedrunTimerModule;
        module.OnLevelChangeDone();
        module.SpawnStartpointTexture();
        module.SpawnEndpointTexture();
    }

    [HarmonyPatch(typeof(GameCore), nameof(GameCore.ChangeScene), typeof(SceneConnectionPoint.ChangeSceneData),
        typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void ChangeScene() {
        var module = DebugModPlus.Instance.SpeedrunTimerModule;
        module.OnLevelChange();
    }


    private Styles? styles;


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


    private SegmentHistory segments = new();

    private void OnLevelChange() {
        EndSegment();
    }

    private void OnLevelChangeDone() {
        if (state is SpeedrunTimerState.Running or SpeedrunTimerState.Loading) {
            SegmentBegin();
        } else if (state == SpeedrunTimerState.StartNextRoom && startRoom != GameCore.Instance.gameLevel.SceneName) {
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
        if (configRecordGhost.Value) {
            ghostSegment = GhostModule.CurrentRecording;
            GhostModule.StopRecording();
        }

        var currentSegment = new SpeedrunRecordingSegment {
            SceneName = GameCore.Instance.gameLevel.SceneName,
            SegmentTime = segmentTime,
            GhostFrames = ghostSegment,
        };
        segments.Add(currentSegment);
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
        segments.Clear();
        startRoom = null;
    }

    public void ResetTimerUser() {
        ToastManager.Toast("Resetting timer");
        ResetTimer();
    }

    public void ResetTimer() {
        stopwatch.Reset();
        currentTime = 0;
        latestTime = 0;
        segmentStartTime = 0;
        segments.Current.Clear();
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

        ResetTimer();
        segments.ClearOld();
    }

    public void SetEndpoint() {
        var endpointPosition = Player.i.transform.position;
        endpointPosition.x += Player.i.Facing == Facings.Right ? 16 : -16;
        var endpointScene = GameCore.Instance.gameLevel.SceneName;
        endpoint = (endpointPosition, endpointScene);

        SpawnEndpointTexture();

        if (state is SpeedrunTimerState.InactiveDone) state = SpeedrunTimerState.Inactive;
        segments.ClearOld();
    }

    public void ClearCheckpoints() {
        startpoint = null;
        endpoint = null;
        if (startpointObject) Object.Destroy(startpointObject);
        if (endpointObject) Object.Destroy(endpointObject);

        ResetTimer();
        segments.ClearOld();
    }

    private void SegmentBegin() {
        Log.Info("Starting segment");

        segmentStartTime = currentTime;
        if (configRecordGhost.Value) GhostModule.StartRecording();

        if (configRecordGhost.Value) {
            if (segments.PB.segments.ElementAtOrDefault(segments.ActiveSegmentIndex) is not
                { GhostFrames: not null } pbSeg) return;

            Log.Info($"Playing back ghost segment of {pbSeg.GhostFrames.Length} frames");
            GhostModule.Playback(pbSeg.GhostFrames);
        }
    }

    public void OnSavestateCreated() {
        // dont reset timer if trigger mode
        if (TimerMode == TimerMode.Triggers) return;
        segments.Clear();
        segmentStartTime = 0;
        startRoom = null;
        if (state is SpeedrunTimerState.InactiveDone) {
            state = SpeedrunTimerState.Inactive;
        }
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

        if (TimerMode == TimerMode.AfterSavestate) {
            SegmentBegin();
        }
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
        state = SpeedrunTimerState.InactiveDone;
        segments.Finish();
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

        var startpointReached = false;
        var endpointReached = false;

        if (doCheckPoints && startpoint is var (sposition, sscene) && sscene == sceneName) {
            var sdistance = Vector2.Distance(playerPosition, sposition);
            if (sdistance < distanceThreshold) startpointReached = true;
        }

        if (doCheckPoints && endpoint is var (eposition, escene) && escene == sceneName) {
            var edistance = Vector2.Distance(playerPosition, eposition);
            if (edistance < distanceThreshold) endpointReached = true;
        }

        if (startpointReached && !endpointReached) StartpointReached();
        if (endpointReached && !startpointReached) EndpointReached();
    }

    public void OnGui() {
        if (state == SpeedrunTimerState.Inactive) return;

        styles ??= new Styles();

        const int paddingX = 8;
        const int paddingY = 14;

        var delta = segments.FinishedPbDelta;

        var timeStr = $"{currentTime:0.00}s";
        var pbStr = segments.PB.FinishedTime is { } pb ? $"PB: {pb:0.00}" : "";

        var deltaStr = delta != 0 && delta is { } d ? FormatTimeDelta(d) : null;

        var timeStyle = state switch {
            SpeedrunTimerState.InactiveDone => (delta is null or < 0) switch {
                true => styles.StyleGold,
                false => styles.StyleGreen,
            },
            _ => styles.Style,
        };

        var sizeTime = LabelSized(timeStyle, timeStr, paddingX, paddingY);
        if (deltaStr != null) {
            LabelSized(styles.StyleDelta, deltaStr, paddingX + sizeTime.x + 6, paddingY, overrideH: sizeTime.y);
        }

        LabelSized(styles.StylePb, pbStr, paddingX, paddingY + sizeTime.y);
    }

    private Vector2 LabelSized(
        GUIStyle style, string text, float x, float y,
        float? overrideW = null,
        float? overrideH = null) {
        var size = style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(x, y, overrideW ?? size.x, overrideH ?? size.y), text, style);
        return size;
    }

    private string FormatTimeDelta(float delta) {
        var sign = delta >= 0 ? "+" : "";
        return $"{sign}{delta:0.00}s";
    }

    public void Destroy() {
        if (startpointObject) Object.Destroy(startpointObject);
        if (endpointObject) Object.Destroy(endpointObject);
    }
}

internal class Styles {
    public GUIStyle Style;
    public GUIStyle StyleGreen;
    public GUIStyle StyleGold;
    public GUIStyle StyleDelta;

    public GUIStyle StylePb;

    public Styles() {
        Style = new GUIStyle(GUI.skin.label) {
            fontStyle = FontStyle.Bold, fontSize = 28, padding = new RectOffset(),
        };
        StyleGreen = new GUIStyle(Style) { normal = { textColor = new Color(0.55f, 0.8f, 0.1f) } };
        StyleGold = new GUIStyle(Style) { normal = { textColor = new Color(1, 0.8f, 0) } };
        StyleDelta = new GUIStyle(Style) {
            fontSize = 22, alignment = TextAnchor.LowerLeft,
            normal = {
                textColor = new Color(0.7f, 0.7f, 0.7f, 10f),
            },
        };
        StylePb = new GUIStyle(GUI.skin.label) {
            fontStyle = FontStyle.Bold, fontSize = 22,
            padding = new RectOffset(),
            normal = {
                textColor = new Color(0.7f, 0.7f, 0.7f, 10f),
            },
        };
    }
}