using System;
using System.Collections.Generic;
using MonoMod.Utils;
using NineSolsAPI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugModPlus.Modules.Hitbox;

[Flags]
public enum HitboxType {
    All = int.MaxValue,
    Default = LEVEL | Player | Interactable | Trigger | AttackSensor | DecreasePosture | Uncategorized,

    Trigger = 1 << 0,

    LEVEL = Terrain | Trigger | PlayerSpawnPoint | MonsterInvisibleWall | Trap | ChangeSceneTrigger | DamageDealer |
            EffectDealer,
    Terrain = 1 << 1,
    Trap = 1 << 2,
    ChangeSceneTrigger = 1 << 3,
    DamageDealer = 1 << 4,
    EffectDealer = 1 << 5,

    MONSTER = AttackSensor | PlayerSensor | ActorSensor | DecreasePosture,
    AttackSensor = 1 << 6,
    PlayerSensor = 1 << 7,
    ActorSensor = 1 << 8,
    DecreasePosture = 1 << 9,
    PlayerSpawnPoint = 1 << 10,
    MonsterInvisibleWall = 1 << 11,

    PLAYER = Player | HookRange | PlayerItemPicker | PlayerHealth | FinderGeneral,
    Player = 1 << 13,
    HookRange = 1 << 14,
    PlayerItemPicker = 1 << 15,
    PlayerHealth = 1 << 16,
    FinderGeneral = 1 << 17,

    SHARED = EffectReceiver | Interactable | PathFindAgent | MonsterPushAway,
    EffectReceiver = 1 << 18,
    Interactable = 1 << 19,
    PathFindAgent = 1 << 20,
    MonsterPushAway = 1 << 21,

    MISC = PathFindTarget | Audio | GeneralFindable,
    PathFindTarget = 1 << 22,
    Audio = 1 << 23,
    GeneralFindable = 1 << 24,

    ActorBody = 1 << 25,
    Todo = 1 << 26,
    Uncategorized = 1 << 31,
}

public class HitboxModule : MonoBehaviour {
    private bool hitboxesVisible;

    public bool HitboxesVisible {
        get => hitboxesVisible;
        set {
            hitboxesVisible = value;
            Reload();
        }
    }

    private static HitboxType Filter => DebugModPlus.Instance.HitboxFilter.Value;

    [BindableMethod(Name = "Toggle Hitboxes", DefaultKeybind = new KeyCode[] { KeyCode.LeftControl, KeyCode.B })]
    private static void ToggleHitboxes() {
        DebugModPlus.Instance.HitboxModule.HitboxesVisible ^= true;
    }

    private void Awake() {
        SceneManager.sceneLoaded += CreateHitboxRender;
        colliders = new SortedDictionary<HitboxType, HashSet<Collider2D>>();
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= CreateHitboxRender;
    }

    private void CreateHitboxRender(Scene scene, LoadSceneMode mode) => Reload();

    #region Drawing

    private static Dictionary<HitboxType, Color> hitboxColors = new() {
        { HitboxType.PlayerItemPicker, new Color(0.8f, 0.4f, 0) },
        { HitboxType.PlayerHealth, new Color(0.4f, 0.8f, 0) },
        { HitboxType.DamageDealer, new Color(0.8f, 0, 0) },
        { HitboxType.PathFindAgent, Color.cyan },
        { HitboxType.Terrain, new Color(0, 0.8f, 0) },
        { HitboxType.Trigger, new Color(0.5f, 0.5f, 1f) },
        { HitboxType.EffectReceiver, new Color(1f, 0.75f, 0.8f) },
        { HitboxType.Trap, new Color(0.0f, 0.0f, 0.5f) },
        { HitboxType.AttackSensor, new Color(0.5f, 0.2f, 0.5f) },
        { HitboxType.Audio, new Color(0.8f, 0.8f, 0.5f) },
        { HitboxType.MonsterPushAway, new Color(0.9f, 0.3f, 0.4f) },
        { HitboxType.PathFindTarget, new Color(0.3f, 0.6f, 0.8f) },
        { HitboxType.EffectDealer, new Color(0.5f, 0.15f, 0.8f) },
        { HitboxType.PlayerSensor, new Color(0.5f, 0.95f, 0.3f) },
        { HitboxType.Player, new Color(0.5f, 0.95f, 0.3f) },
        { HitboxType.ActorSensor, new Color(0.1f, 0.95f, 0.3f) },
        { HitboxType.GeneralFindable, new Color(0.8f, 0.15f, 0.3f) },
        { HitboxType.ChangeSceneTrigger, new Color(0.8f, 0.85f, 0.3f) },
        { HitboxType.FinderGeneral, new Color(0.8f, 0.85f, 0.8f) },
        { HitboxType.HookRange, new Color(0.3f, 0.85f, 0.8f) },
        { HitboxType.Interactable, new Color(0.8f, 0.25f, 0.8f) },
        { HitboxType.Uncategorized, new Color(0.9f, 0.6f, 0.4f) },
        { HitboxType.ActorBody, new Color(1, 0.5f, 1) },
        { HitboxType.DecreasePosture, new Color(0.4f, 0.5f, 1) },
        { HitboxType.PlayerSpawnPoint, new Color(0.65f, 0.8f, 0.6f) },
        { HitboxType.MonsterInvisibleWall, new Color(0.65f, 0.3f, 0.6f) },

        { HitboxType.Todo, new Color(1, 0, 1) },
    };

    private static Dictionary<HitboxType, int> depths = new() {
        { HitboxType.Player, 0 },
        { HitboxType.PlayerHealth, 0 },
        { HitboxType.PlayerItemPicker, 0 },
        { HitboxType.DamageDealer, 1 },
        { HitboxType.PathFindAgent, 2 },
        { HitboxType.Terrain, 3 },
        { HitboxType.Trigger, 4 },
        { HitboxType.EffectReceiver, 5 },
        { HitboxType.Trap, 6 },
        { HitboxType.AttackSensor, 7 },
        { HitboxType.Audio, 8 },
        { HitboxType.MonsterPushAway, 9 },
        { HitboxType.PathFindTarget, 10 },
        { HitboxType.EffectDealer, 11 },
        { HitboxType.PlayerSensor, 12 },
        { HitboxType.ActorSensor, 13 },
        { HitboxType.GeneralFindable, 14 },
        { HitboxType.ChangeSceneTrigger, 15 },
        { HitboxType.FinderGeneral, 16 },
        { HitboxType.HookRange, 17 },
        { HitboxType.Interactable, 18 },
        { HitboxType.ActorBody, 19 },
        { HitboxType.DecreasePosture, 19 },
        { HitboxType.PlayerSpawnPoint, 20 },
        { HitboxType.MonsterInvisibleWall, 20 },
        { HitboxType.Todo, 21 },
        { HitboxType.Uncategorized, 32 },
    };

    private static int layerMonsterInvisibleWall = LayerMask.NameToLayer("MonsterInvisibleWall");


    private SortedDictionary<HitboxType, HashSet<Collider2D>> colliders = null!;

    // public static float LineWidth => Math.Max(0.7f, Screen.width / 960f * GameCameras.instance.tk2dCam.ZoomFactor);
    private static float LineWidth => Math.Max(0.7f, Screen.width / 2000f);

    private void Reload() {
        colliders.Clear();
        try {
            foreach (var col in Resources.FindObjectsOfTypeAll<Collider2D>()) TryAddHitboxes(col);
        } catch (Exception e) {
            ToastManager.Toast(e.ToString());
        }
    }

    public void UpdateHitbox(GameObject go) {
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) TryAddHitboxes(col);
    }

    private static Vector2 LocalToScreenPoint(Camera camera, Collider2D collider2D, Vector2 point) {
        Vector2 result =
            camera.WorldToScreenPoint((Vector2)collider2D.transform.TransformPoint(point + collider2D.offset));
        return new Vector2((int)Math.Round(result.x), (int)Math.Round(Screen.height - result.y));
    }

    private void TryAddHitboxes(Collider2D collider2D) {
        if (collider2D == null) return;

        if (collider2D is BoxCollider2D or PolygonCollider2D or EdgeCollider2D or CircleCollider2D) {
            var go = collider2D.gameObject;

            if (collider2D.GetComponent<Player>())
                colliders.AddToKey(HitboxType.Player, collider2D);
            if (collider2D.GetComponent<MonsterPushAway>())
                colliders.AddToKey(HitboxType.MonsterPushAway, collider2D);
            else if (collider2D.GetComponent<Trap>())
                colliders.AddToKey(HitboxType.Trap, collider2D);
            else if (collider2D.GetComponent<DamageDealer>())
                colliders.AddToKey(HitboxType.DamageDealer, collider2D);
            else if (collider2D.GetComponent<TriggerDetector>())
                colliders.AddToKey(HitboxType.Trigger, collider2D);
            else if (collider2D.GetComponent<AttackSensor>())
                colliders.AddToKey(HitboxType.AttackSensor, collider2D);
            else if (collider2D.GetComponent<AkGameObj>())
                colliders.AddToKey(HitboxType.Audio, collider2D);
            else if (collider2D.GetComponent<PathArea>())
                colliders.AddToKey(HitboxType.Terrain, collider2D);
            else if (collider2D.GetComponent<EffectReceiver>())
                colliders.AddToKey(HitboxType.EffectReceiver, collider2D);
            else if (collider2D.GetComponent<EffectDealer>())
                colliders.AddToKey(HitboxType.EffectDealer, collider2D);
            else if (collider2D.GetComponent<PathFindAgent>())
                colliders.AddToKey(HitboxType.PathFindAgent, collider2D);
            else if (collider2D.GetComponent<PlayerSensor>())
                colliders.AddToKey(HitboxType.PlayerSensor, collider2D);
            else if (collider2D.GetComponent<ActorSensor>())
                colliders.AddToKey(HitboxType.ActorSensor, collider2D);
            else if (collider2D.GetComponent<PathFindTarget>())
                colliders.AddToKey(HitboxType.PathFindTarget, collider2D);
            else if (collider2D.GetComponent<GeneralFindable>())
                colliders.AddToKey(HitboxType.GeneralFindable, collider2D);
            else if (collider2D.GetComponent<ChangeSceneTrigger>())
                colliders.AddToKey(HitboxType.ChangeSceneTrigger, collider2D);
            else if (collider2D.GetComponent<HookableFinder>())
                colliders.AddToKey(HitboxType.HookRange, collider2D);
            else if (collider2D.GetComponent<GeneralFinder>())
                colliders.AddToKey(HitboxType.FinderGeneral, collider2D);
            else if (collider2D.GetComponent<ItemPicker>())
                colliders.AddToKey(HitboxType.PlayerItemPicker, collider2D); // Todo
            else if (collider2D.GetComponent<InteractableArea>())
                colliders.AddToKey(HitboxType.Interactable, collider2D);
            else if (collider2D.GetComponent<InteractableFinder>())
                colliders.AddToKey(HitboxType.Interactable, collider2D);
            else if (collider2D.GetComponent<PlayerHealth>())
                colliders.AddToKey(HitboxType.PlayerHealth, collider2D);
            else if (collider2D.GetComponent<FootStepSwitch>())
                colliders.AddToKey(HitboxType.Audio, collider2D);
            else if (collider2D.GetComponent<AkEnvironment>())
                colliders.AddToKey(HitboxType.Audio, collider2D);
            else if (collider2D.GetComponent<ActorBody>())
                colliders.AddToKey(HitboxType.ActorBody, collider2D);
            else if (collider2D.GetComponent<MonsterDecreasePostureReceiver>())
                colliders.AddToKey(HitboxType.DecreasePosture, collider2D);
            else if (collider2D.GetComponent<PlayerSpawnPoint>())
                colliders.AddToKey(HitboxType.PlayerSpawnPoint, collider2D);
            else if (go.layer == layerMonsterInvisibleWall)
                colliders.AddToKey(HitboxType.MonsterInvisibleWall, collider2D);
            // 
            else if (collider2D.GetComponent<ParticleSystem>())
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (collider2D.GetComponent<PathPoint>())
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (go.name == "PlatformPhysics")
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (go.name == "BlockWall")
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (go.name.StartsWith("shatterglass"))
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (go.name.StartsWith("oneway"))
                colliders.AddToKey(HitboxType.Todo, collider2D);
            else if (go.name == "backSide") {
                // colliders.AddToKey(HitboxType.Todo, collider2D);
            } else if (go.name == "BoxRoot") {
                // colliders.AddToKey(HitboxType.Uncategorized, collider2D);
            } else {
                // ToastManager.Toast(go.name);
                colliders.AddToKey(HitboxType.Uncategorized, collider2D);
            }
            /*} else if (go.name == "BoxRoot") {
            } else if (go.name.Contains("EffectReceivingCollider")) {
            } else if (go.name.Contains("STfog")) {
            } else if (go.name.Contains("Physics")) {
            } else if (go.name.Contains("HiddenPath")) {
            } else if (go.name.Contains("audio")) {
            } else if (go.name.Contains("Deadbody")) {
            } else if (go.name.Contains("ScanableFinders")) {
            } else if (go.name.Contains("monster finder")) {
            } else if (go.name.Contains("ShieldBreak")) {
            } else if (go.name.Contains("Drop")) {
            } else if (go.name.Contains("Arrow")) {
            } else if (go.name.Contains("rayCast")) {
            } else if (go.name.Contains("debris")) {
            } else if (go.name.Contains("QCpart")) {
            } else if (go.name.Contains("TrapDamage")) {
            } else if (go.name.Contains("scanner")) {
            } else if (go.name.Contains("HackDrone")) {
            } else if (go.name.Contains("Foo")) {
            } else if (go.name.Contains("Shield")) {
            } else if (go.name.Contains("LampFinder")) {
            } else if (go.name.Contains("LightSourceFinder")) {*/
        }
    }

    public void OnGUI() {
        if (!HitboxesVisible) return;

        try {
            if (Event.current?.type != EventType.Repaint || !GameCore.IsAvailable()) return;
            if (GameCore.Instance.gameLevel is not { } level) return;

            GUI.depth = int.MaxValue;
            var camera = level.sceneCamera;
            // var camera = CameraManager.Instance.camera2D.GameCamera;

            var lineWidth = LineWidth;
            foreach (var (type, typeColliders) in colliders) {
                if (!Filter.HasFlag(type)) {
                    continue;
                }

                foreach (var collider2D in typeColliders) {
                    DrawHitbox(camera, collider2D, type, lineWidth);
                }
            }
        } catch (Exception e) {
            ToastManager.Toast(e);
        }
    }

    private void DrawHitbox(Camera camera, Collider2D collider2D, HitboxType hitboxType, float lineWidth) {
        if (!collider2D || !collider2D.isActiveAndEnabled) return;

        var origDepth = GUI.depth;
        GUI.depth = depths[hitboxType];
        if (collider2D is BoxCollider2D or EdgeCollider2D or PolygonCollider2D) {
            switch (collider2D) {
                case BoxCollider2D boxCollider2D:
                    var halfSize = boxCollider2D.size / 2f;
                    Vector2 topLeft = new(-halfSize.x, halfSize.y);
                    var topRight = halfSize;
                    Vector2 bottomRight = new(halfSize.x, -halfSize.y);
                    var bottomLeft = -halfSize;
                    var boxPoints = new List<Vector2> {
                        topLeft, topRight, bottomRight, bottomLeft, topLeft,
                    };
                    DrawPointSequence(boxPoints, camera, collider2D, hitboxType, lineWidth);
                    break;
                case EdgeCollider2D edgeCollider2D:
                    DrawPointSequence(new List<Vector2>(edgeCollider2D.points),
                        camera,
                        collider2D,
                        hitboxType,
                        lineWidth);
                    break;
                case PolygonCollider2D polygonCollider2D:
                    for (var i = 0; i < polygonCollider2D.pathCount; i++) {
                        List<Vector2> polygonPoints = new(polygonCollider2D.GetPath(i));
                        if (polygonPoints.Count > 0) polygonPoints.Add(polygonPoints[0]);
                        DrawPointSequence(polygonPoints, camera, collider2D, hitboxType, lineWidth);
                    }

                    break;
            }
        } else if (collider2D is CircleCollider2D circleCollider2D) {
            var center = LocalToScreenPoint(camera, collider2D, Vector2.zero);
            var right = LocalToScreenPoint(camera, collider2D, Vector2.right * circleCollider2D.radius);
            var radius = (int)Math.Round(Vector2.Distance(center, right));
            Drawing.DrawCircle(center,
                radius,
                hitboxColors[hitboxType],
                lineWidth,
                true,
                Mathf.Clamp(radius / 16, 4, 32));
        }

        GUI.depth = origDepth;
    }

    private void DrawPointSequence(List<Vector2> points, Camera camera, Collider2D collider2D,
        HitboxType hitboxType,
        float lineWidth) {
        for (var i = 0; i < points.Count - 1; i++) {
            var pointA = LocalToScreenPoint(camera, collider2D, points[i]);
            var pointB = LocalToScreenPoint(camera, collider2D, points[i + 1]);
            Drawing.DrawLine(pointA, pointB, hitboxColors[hitboxType], lineWidth, true);
        }
    }

    #endregion
}

internal static class CollectionExtensions {
    public static void AddToKey<TKey, TValue>(this IDictionary<TKey, List<TValue>> dict, TKey key,
        TValue value) {
        if (dict.TryGetValue(key, out var list)) {
            list.Add(value);
            return;
        }

        dict[key] = [value];
    }

    public static void AddToKey<TKey, TValue>(this IDictionary<TKey, HashSet<TValue>> dict, TKey key,
        TValue value) {
        if (dict.TryGetValue(key, out var list)) {
            list.Add(value);
            return;
        }

        dict[key] = [value];
    }
}