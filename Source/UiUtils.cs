using UnityEngine;

namespace DebugModPlus;

public static class UiUtils {
    public static Texture2D GetColorTexture(Color color) {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}