#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using HarmonyLib;
using NineSolsAPI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugMod.Modules;

internal enum TimerMode {
    AfterSavestate,
    NextRoom,
}

internal enum SpeedrunTimerState {
    Inactive,
    Running,
    StartNextRoom,
}

[HarmonyPatch]
public class SpeedrunTimerModule {
    private static bool isLoading = false;

    private const bool EnableGhost = false;

    [HarmonyPatch(typeof(GameCore), "InitializeGameLevel")]
    [HarmonyPostfix]
    private static void InitializeGameLevel() {
        isLoading = false;
        var module = DebugMod.Instance.SpeedrunTimerModule;
        if (module.startRoom != GameCore.Instance.gameLevel.SceneName) module.OnLevelChangeDone();

        module.SpawnEndpointTexture();
    }

    [HarmonyPatch(typeof(GameCore), nameof(GameCore.ChangeScene), typeof(SceneConnectionPoint.ChangeSceneData),
        typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void ChangeScene() {
        isLoading = true;
        var module = DebugMod.Instance.SpeedrunTimerModule;
        module.OnLevelChange();
    }

    private GUIStyle? style;


    // state
    private float segmentStartTime = 0;
    private float time = 0;

    private string? startRoom = null;
    private TimerMode timerMode = TimerMode.AfterSavestate;
    private SpeedrunTimerState state = SpeedrunTimerState.Inactive;
    private (Vector2, string)? endpoint = null;


    private bool done = false;
    private List<(string, float, GhostFrame[]?)>? lastSegments = null;
    private List<(string, float, GhostFrame[]?)> currentSegments = [];
    private float? lastTimeDelta;

    private void OnLevelChange() {
        EndSegment();
    }

    private void OnLevelChangeDone() {
        SegmentBegin();

        if (state == SpeedrunTimerState.StartNextRoom && startRoom != GameCore.Instance.gameLevel.SceneName)
            state = SpeedrunTimerState.Running;
    }

    private GhostModule GhostModule => DebugMod.Instance.GhostModule;

    private void EndSegment() {
        if (state != SpeedrunTimerState.Running) return;

        var roomTime = time - segmentStartTime;
        segmentStartTime = time;

        Log.Info($"Ending segment of {roomTime:0.00}s {lastSegments?.Count}");

        GhostFrame[]? ghostSegment = null;
        if (EnableGhost) {
            ghostSegment = GhostModule.CurrentRecording;
            GhostModule.StopRecording();
        }

        currentSegments.Add((GameCore.Instance.gameLevel.SceneName, roomTime, ghostSegment));

        if (lastSegments is not null) {
            var i = 0;

            string? lastRoom;
            float? lastTime = null;
            float? currentTime = null;

            while (true) {
                Log.Info(
                    $"{i} curr {currentSegments.Count} last {(lastSegments != null ? lastSegments.Count.ToString() : "null")}");
                if (i >= currentSegments.Count) {
                    lastTimeDelta = currentTime - lastTime;
                    Log.Info($"lasttiemdelta of {lastTimeDelta:0.00}s");
                    break;
                }

                if (i >= lastSegments.Count) break;

                (lastRoom, lastTime, _) = lastSegments[i];
                (var currentRoom, currentTime, _) = currentSegments[i];

                if (lastRoom != currentRoom) break;

                i++;
            }
        }
    }

    private (string, float, GhostFrame[]?)? GetMatchingLastSegment() {
        if (lastSegments is null) return null;

        (string, float, GhostFrame[]?)? lastSegment = null;
        for (var i = 0;; i++) {
            if (i >= lastSegments.Count) return null;

            lastSegment = lastSegments[i];

            if (i >= currentSegments.Count) return lastSegment;
        }
    }


    private void SegmentBegin() {
        segmentStartTime = time;
        if (EnableGhost) GhostModule.StartRecording();

        if (EnableGhost && GetMatchingLastSegment() is (_, _, { } ghostFrames)) {
            ToastManager.Toast($"playing back {ghostFrames.Length} frames");
            GhostModule.Playback(ghostFrames);
        }
    }

    public void OnSavestateCreated() {
        state = SpeedrunTimerState.Inactive;
        done = false;
        currentSegments = [];
        lastSegments = null;
        lastTimeDelta = null;
        segmentStartTime = 0;
    }

    public void OnSavestateLoaded() {
        time = 0;
        segmentStartTime = 0;
        done = false;
        currentSegments = [];
        lastTimeDelta = null;

        startRoom = GameCore.Instance.gameLevel.SceneName;
        state = timerMode switch {
            TimerMode.AfterSavestate => SpeedrunTimerState.Running,
            TimerMode.NextRoom => SpeedrunTimerState.StartNextRoom,
            _ => throw new ArgumentOutOfRangeException(),
        };

        SegmentBegin();
    }

    private Sprite? endpointSprite;
    private GameObject? endpointObject;

    private void SpawnEndpointTexture() {
        if (endpoint is not var (position, scene)) return;

        if (GameCore.Instance.gameLevel.SceneName != scene) return;

        if (endpointSprite == null) {
            // ReSharper disable once Unity.UnknownResource
            var texture = Resources.LoadAll<Texture2D>("/").First(t => t.name == "checkmark");
            endpointSprite = Sprite.CreateSprite(
                texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), 16f, 0,
                SpriteMeshType.FullRect, Vector4.zero, false, []
            );
        }


        if (endpointObject) Object.Destroy(endpointObject);
        endpointObject = new GameObject("flag") {
            transform = {
                position = position,
            },
        };
        var spriteRenderer = endpointObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = endpointSprite;
        spriteRenderer.material.shader = Shader.Find("GUI/Text Shader");
        spriteRenderer.material.color = new Color(0.8f, 0.2f, 1, 1);
    }

    public void SetEndpoint() {
        var endpointPosition = Player.i.transform.position;
        var endpointScene = GameCore.Instance.gameLevel.SceneName;
        endpoint = (endpointPosition, endpointScene);

        SpawnEndpointTexture();
    }

    private void EndpointReached() {
        if (state != SpeedrunTimerState.Running) return;

        EndSegment();
        done = true;
        state = SpeedrunTimerState.Inactive;
        lastSegments = currentSegments;
        ToastManager.Toast($"endpoint reached with {currentSegments.Count}");
        currentSegments = [];
    }

    public void CycleTimerMode() {
        timerMode = (TimerMode)((int)(timerMode + 1) % 2);
        ToastManager.Toast(timerMode);
    }

    public void LateUpdate() {
        try {
            if (state == SpeedrunTimerState.Running && !isLoading)
                time += RCGTime.deltaTime;

            if (endpoint is var (position, scene) && scene == GameCore.Instance.gameLevel.SceneName) {
                const float distanceThreshold = 20;
                var distance = Vector2.Distance(Player.i.transform.position, position);
                if (distance < distanceThreshold) EndpointReached();
            }
        } catch (Exception e) {
            Log.Error($"Error during speedruntime LateUpdate: {e}");
        }
    }


    public void OnGui() {
        const int padding = 8;

        if (done || state != SpeedrunTimerState.Inactive) {
            style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 30 };
            var timeStr = $"{(done ? "Done in " : "")}{time:0.0}s";
            if (lastTimeDelta is not null) {
                var diffText = $"{(lastTimeDelta > 0 ? "+" : "")}{lastTimeDelta:0.0}s";
                timeStr += $"\nCompared to last: {diffText}";
            }

            GUI.Label(new Rect(padding, padding, 600, 100), timeStr, style);
        }
    }

    public void Destroy() {
        if (endpointObject) Object.Destroy(endpointObject);
    }
}