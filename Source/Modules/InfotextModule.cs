using System;
using NineSolsAPI;
using TAS;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus.Modules;

public class InfotextModule {
    private static bool infotextActive = false;
    private TMP_Text debugCanvasInfoText;

    public InfotextModule() {
        var debugText = new GameObject("Info Text");
        debugText.transform.SetParent(NineSolsAPICore.FullscreenCanvas.gameObject.transform);
        debugCanvasInfoText = debugText.AddComponent<TextMeshProUGUI>();
        debugCanvasInfoText.alignment = TextAlignmentOptions.TopLeft;
        debugCanvasInfoText.fontSize = 20;
        debugCanvasInfoText.color = Color.white;

        var debugTextTransform = debugCanvasInfoText.GetComponent<RectTransform>();
        debugTextTransform.anchorMin = new Vector2(0, 1);
        debugTextTransform.anchorMax = new Vector2(0, 1);
        debugTextTransform.pivot = new Vector2(0f, 1f);
        debugTextTransform.anchoredPosition = new Vector2(10, -10);
        debugTextTransform.sizeDelta = new Vector2(800f, 0f);

        RCGLifeCycle.DontDestroyForever(debugText);
    }

    [BindableMethod(Name = "Toggle Infotext")]
    private static void ToggleFreecam() {
        infotextActive = !infotextActive;
    }

    public void Update() {
        if (!infotextActive) {
            debugCanvasInfoText.text = "";
            return;
        }

        try {
            debugCanvasInfoText.text = GameInfo.GetInfoText();

            debugCanvasInfoText.text += "\n" + GameInfo.GetMonsterInfotext();
        } catch (Exception e) {
            Log.Error(e);
        }
    }

    public void Destroy() {
        Object.Destroy(debugCanvasInfoText.gameObject);
    }
}