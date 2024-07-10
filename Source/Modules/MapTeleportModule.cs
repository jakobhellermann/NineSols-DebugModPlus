using System;
using NineSolsAPI;
using RCGSetting;
using UnityEngine;

namespace DebugMod.Modules;

public static class MapTeleportModule {
    public static void Update() {
        var forceReloadScene = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (Input.GetMouseButtonDown(0) && UIManager.IsAvailable())
            try {
                TeleportToMap(Input.mousePosition, forceReloadScene);
            } catch (Exception e) {
                ToastManager.Toast($"Could not teleport: {e}");
            }
    }

    // ReSharper disable Unity.PerformanceAnalysis
    private static void TeleportToMap(Vector2 screenPosition, bool forceReloadScene) {
        var mapPanelController = UIManager.Instance.mapPanelController;
        var minimap = mapPanelController.completeMapPanel.Minimap;
        if (!minimap.isActiveAndEnabled) return;

        var minimapRect = minimap.ImageMaskRoot;
        var gameplayUiCam = UIManager.Instance.gameObject.GetComponentInChildren<Camera>();

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                minimapRect, screenPosition, gameplayUiCam, out var localPoint)) return;
        var clickRatio = localPoint / minimapRect.sizeDelta;
        var worldPosition = minimap.MapData.ImageRatioToWorldPosition(clickRatio);

        TeleportTo(worldPosition, minimap.MapData, forceReloadScene);
    }

    private static void TeleportTo(Vector2 worldPosition, GameLevelMapData mapData, bool forceReloadScene) {
        UIManager.Instance.menuUI.HideMenu();

        var isCurrentScene = mapData.sceneID == GameCore.Instance.CurrentSceneName;

        if (!isCurrentScene || forceReloadScene)
            GoToScene(mapData.sceneID, worldPosition, true);
        else
            Player.i.transform.position = worldPosition;
    }

    private static void GoToScene(string sceneName, Vector3 worldPosition, bool showTip = false) {
        var changeSceneData = new SceneConnectionPoint.ChangeSceneData {
            sceneName = sceneName,
            playerSpawnPosition = () => worldPosition,
            changeSceneMode = SceneConnectionPoint.ChangeSceneMode.Teleport,
        };
        GameCore.Instance.ChangeScene(changeSceneData, showTip);
    }
}

internal static class Extensions {
    public static Vector2 ImageRatioToWorldPosition(this GameLevelMapData levelData, Vector2 imageRatio) {
        var num1 = (imageRatio.x - levelData.ImageMapRatioMinX) /
                   (levelData.ImageMapRatioMaxX - levelData.ImageMapRatioMinX);
        var num2 = (imageRatio.y - levelData.ImageMapRatioMinY) /
                   (levelData.ImageMapRatioMaxY - levelData.ImageMapRatioMinY);
        var worldX = num1 * levelData.MapWidth + levelData.MapMinX;
        var worldY = num2 * levelData.MapHeight + levelData.MapMinY;
        return new Vector2(worldX, worldY);
    }
}