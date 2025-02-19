using System;
using System.Collections.Generic;
using NineSolsAPI;
using UnityEngine;

namespace DebugMod.Modules.Hitbox;

public class HitboxRender : MonoBehaviour {
    // ReSharper disable once StructCanBeMadeReadOnly
    private struct HitboxType : IComparable<HitboxType> {
        public static readonly HitboxType Player = new(Color.yellow, 0);
        public static readonly HitboxType Enemy = new(new Color(0.8f, 0, 0), 1);
        public static readonly HitboxType PathFindAgent = new(Color.cyan, 2);
        public static readonly HitboxType Terrain = new(new Color(0, 0.8f, 0), 3);
        public static readonly HitboxType Trigger = new(new Color(0.5f, 0.5f, 1f), 4);
        public static readonly HitboxType EffectReceiver = new(new Color(1f, 0.75f, 0.8f), 5);
        public static readonly HitboxType Trap = new(new Color(0.0f, 0.0f, 0.5f), 6);
        public static readonly HitboxType AttackSensor = new(new Color(0.5f, 0.0f, 0.5f), 7);
        public static readonly HitboxType AkGameObj = new(new Color(0.8f, 0.8f, 0.5f), 8);
        public static readonly HitboxType MonsterPushAway = new(new Color(0.9f, 0.3f, 0.4f), 9);
        public static readonly HitboxType PathFindTarget = new(new Color(0.3f, 0.6f, 0.8f), 10);
        public static readonly HitboxType EffectDealer = new(new Color(0.5f, 0.15f, 0.8f), 11);
        public static readonly HitboxType PlayerSensor = new(new Color(0.5f, 0.95f, 0.3f), 12);
        public static readonly HitboxType ActorSensor = new(new Color(0.1f, 0.95f, 0.3f), 13);
        public static readonly HitboxType GeneralFindable = new(new Color(0.8f, 0.15f, 0.3f), 14);
        public static readonly HitboxType ChangeSceneTrigger = new(new Color(0.8f, 0.85f, 0.3f), 15);
        public static readonly HitboxType Finder = new(new Color(0.8f, 0.85f, 0.8f), 16);
        public static readonly HitboxType Interactable = new(new Color(0.8f, 0.25f, 0.8f), 17);
        public static readonly HitboxType Other = new(new Color(0.9f, 0.6f, 0.4f), 18);

        public readonly Color Color;
        public readonly int Depth;

        private HitboxType(Color color, int depth) {
            Color = color;
            Depth = depth;
        }

        public int CompareTo(HitboxType other) => other.Depth.CompareTo(Depth);
    }

    private readonly SortedDictionary<HitboxType, HashSet<Collider2D>> colliders = new() {
        { HitboxType.ActorSensor, new HashSet<Collider2D>() },
        { HitboxType.AkGameObj, new HashSet<Collider2D>() },
        { HitboxType.AttackSensor, new HashSet<Collider2D>() },
        { HitboxType.ChangeSceneTrigger, new HashSet<Collider2D>() },
        { HitboxType.EffectDealer, new HashSet<Collider2D>() },
        { HitboxType.EffectReceiver, new HashSet<Collider2D>() },
        { HitboxType.Enemy, new HashSet<Collider2D>() },
        { HitboxType.GeneralFindable, new HashSet<Collider2D>() },
        { HitboxType.MonsterPushAway, new HashSet<Collider2D>() },
        { HitboxType.Other, new HashSet<Collider2D>() },
        { HitboxType.PathFindAgent, new HashSet<Collider2D>() },
        { HitboxType.PathFindTarget, new HashSet<Collider2D>() },
        { HitboxType.Player, new HashSet<Collider2D>() },
        { HitboxType.PlayerSensor, new HashSet<Collider2D>() },
        { HitboxType.Terrain, new HashSet<Collider2D>() },
        { HitboxType.Trap, new HashSet<Collider2D>() },
        { HitboxType.Trigger, new HashSet<Collider2D>() },
        { HitboxType.Interactable, new HashSet<Collider2D>() },
        { HitboxType.Finder, new HashSet<Collider2D>() },
    };

    // public static float LineWidth => Math.Max(0.7f, Screen.width / 960f * GameCameras.instance.tk2dCam.ZoomFactor);
    public static float LineWidth => Math.Max(0.7f, Screen.width / 2000f);

    private void Start() {
        try {
            foreach (var col in Resources.FindObjectsOfTypeAll<Collider2D>()) TryAddHitboxes(col);
        } catch (Exception e) {
            ToastManager.Toast(e.ToString());
        }
    }

    public void UpdateHitbox(GameObject go) {
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) TryAddHitboxes(col);
    }

    private Vector2 LocalToScreenPoint(Camera camera, Collider2D collider2D, Vector2 point) {
        Vector2 result =
            camera.WorldToScreenPoint((Vector2)collider2D.transform.TransformPoint(point + collider2D.offset));
        return new Vector2((int)Math.Round(result.x), (int)Math.Round(Screen.height - result.y));
    }

    private void TryAddHitboxes(Collider2D collider2D) {
        if (collider2D == null) return;

        if (collider2D is BoxCollider2D or PolygonCollider2D or EdgeCollider2D or CircleCollider2D) {
            var go = collider2D.gameObject;

            if (collider2D.GetComponent<MonsterPushAway>())
                colliders[HitboxType.MonsterPushAway].Add(collider2D);
            else if (collider2D.GetComponent<Trap>())
                colliders[HitboxType.Trap].Add(collider2D);
            else if (collider2D.GetComponent<DamageDealer>())
                colliders[HitboxType.Enemy].Add(collider2D);
            else if (collider2D.GetComponent<TriggerDetector>())
                colliders[HitboxType.Trigger].Add(collider2D);
            else if (collider2D.GetComponent<AttackSensor>())
                colliders[HitboxType.AttackSensor].Add(collider2D);
            else if (collider2D.GetComponent<AkGameObj>())
                colliders[HitboxType.AkGameObj].Add(collider2D);
            else if (collider2D.GetComponent<PathArea>())
                colliders[HitboxType.Terrain].Add(collider2D);
            else if (collider2D.GetComponent<EffectReceiver>())
                colliders[HitboxType.EffectReceiver].Add(collider2D);
            else if (collider2D.GetComponent<EffectDealer>())
                colliders[HitboxType.EffectDealer].Add(collider2D);
            else if (collider2D.GetComponent<PathFindAgent>())
                colliders[HitboxType.PathFindAgent].Add(collider2D);
            else if (collider2D.GetComponent<PlayerSensor>())
                colliders[HitboxType.PlayerSensor].Add(collider2D);
            else if (collider2D.GetComponent<ActorSensor>())
                colliders[HitboxType.ActorSensor].Add(collider2D);
            else if (collider2D.GetComponent<PathFindTarget>())
                colliders[HitboxType.PathFindTarget].Add(collider2D);
            else if (collider2D.GetComponent<GeneralFindable>())
                colliders[HitboxType.GeneralFindable].Add(collider2D);
            else if (collider2D.GetComponent<ChangeSceneTrigger>())
                colliders[HitboxType.ChangeSceneTrigger].Add(collider2D);
            else if (collider2D.GetComponent<Player>())
                colliders[HitboxType.Player].Add(collider2D);
            else if (collider2D.GetComponent<HookableFinder>())
                colliders[HitboxType.Finder].Add(collider2D);
            else if (collider2D.GetComponent<GeneralFinder>())
                colliders[HitboxType.Finder].Add(collider2D);
            else if (collider2D.GetComponent<ItemPicker>())
                colliders[HitboxType.Player].Add(collider2D); //Todo
            else if (collider2D.GetComponent<InteractableArea>())
                colliders[HitboxType.Interactable].Add(collider2D);
            else if (collider2D.GetComponent<InteractableFinder>())
                colliders[HitboxType.Interactable].Add(collider2D);
            else if (collider2D.GetComponent<Health>())
                colliders[HitboxType.Player].Add(collider2D);
            else if (go.GetComponents<object>().Length == 0) {
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
            } else
                //if (go.name.Contains("Interactable"))
                // Plugin.Instance.ToastManager.Toast(go.GetComponents<object>()[0]);
                colliders[HitboxType.Other].Add(collider2D);

            /*if (collider2D.GetComponent<DamageHero>() || collider2D.gameObject.LocateMyFSM("damages_hero"))
                colliders[HitboxType.Enemy].Add(collider2D);
            else if (go.GetComponent<HealthManager>() || go.LocateMyFSM("health_manager_enemy") ||
                     go.LocateMyFSM("health_manager"))
                colliders[HitboxType.Other].Add(collider2D);
            else if (go.layer == (int)PhysLayers.TERRAIN) {
                if (go.name.Contains("Breakable") || go.name.Contains("Collapse") ||
                    go.GetComponent<Breakable>() != null) colliders[HitboxType.Breakable].Add(collider2D);
                else colliders[HitboxType.Terrain].Add(collider2D);
            } else if (go == HeroController.instance?.gameObject && !collider2D.isTrigger)
                colliders[HitboxType.Player].Add(collider2D);
            else if (go.GetComponent<DamageEnemies>() || go.LocateMyFSM("damages_enemy") ||
                     (go.name == "Damager" && go.LocateMyFSM("Damage")))
                colliders[HitboxType.Attack].Add(collider2D);
            else if (collider2D.isTrigger && collider2D.GetComponent<HazardRespawnTrigger>())
                colliders[HitboxType.HazardRespawn].Add(collider2D);
            else if (collider2D.isTrigger && collider2D.GetComponent<TransitionPoint>())
                colliders[HitboxType.Gate].Add(collider2D);
            else if (collider2D.GetComponent<Breakable>()) {
                NonBouncer bounce = collider2D.GetComponent<NonBouncer>();
                if (bounce == null || !bounce.active) colliders[HitboxType.Trigger].Add(collider2D);
            } else if (HitboxViewer.State == 2) colliders[HitboxType.Other].Add(collider2D);*/
        }
    }

    private void OnGUI() {
        if (Event.current?.type != EventType.Repaint || !SingletonBehaviour<GameCore>.IsAvailable()) return;


        GUI.depth = int.MaxValue;
        // var camera = CameraManager.Instance.camera2D.GameCamera;
        var camera = GameCore.Instance.gameLevel.sceneCamera;

        var lineWidth = LineWidth;
        foreach (var pair in colliders)
        foreach (var collider2D in pair.Value)
            DrawHitbox(camera, collider2D, pair.Key, lineWidth);
    }

    private void DrawHitbox(Camera camera, Collider2D collider2D, HitboxType hitboxType, float lineWidth) {
        if (collider2D == null || !collider2D.isActiveAndEnabled) return;

        var origDepth = GUI.depth;
        GUI.depth = hitboxType.Depth;
        if (collider2D is BoxCollider2D or EdgeCollider2D or PolygonCollider2D)
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
                    DrawPointSequence(new List<Vector2>(edgeCollider2D.points), camera, collider2D, hitboxType,
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
        else if (collider2D is CircleCollider2D circleCollider2D) {
            var center = LocalToScreenPoint(camera, collider2D, Vector2.zero);
            var right = LocalToScreenPoint(camera, collider2D, Vector2.right * circleCollider2D.radius);
            var radius = (int)Math.Round(Vector2.Distance(center, right));
            Drawing.DrawCircle(center, radius, hitboxType.Color, lineWidth, true, Mathf.Clamp(radius / 16, 4, 32));
        }

        GUI.depth = origDepth;
    }

    private void DrawPointSequence(List<Vector2> points, Camera camera, Collider2D collider2D,
        HitboxType hitboxType,
        float lineWidth) {
        for (var i = 0; i < points.Count - 1; i++) {
            var pointA = LocalToScreenPoint(camera, collider2D, points[i]);
            var pointB = LocalToScreenPoint(camera, collider2D, points[i + 1]);
            Drawing.DrawLine(pointA, pointB, hitboxType.Color, lineWidth, true);
        }
    }
}