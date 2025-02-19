using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugModPlus.Modules.Hitbox;

public class HitboxModule {
    private static bool hitboxesVisible;

    private HitboxRender hitboxRender;

    [BindableMethod(Name = "Toggle Hitboxes", DefaultKeybind = new KeyCode[] { KeyCode.LeftControl, KeyCode.B })]
    private static void ToggleHitboxes() {
        hitboxesVisible = !hitboxesVisible;

        if (hitboxesVisible)
            DebugModPlus.Instance.HitboxModule.Load();
        else
            DebugModPlus.Instance.HitboxModule.Unload();
    }

    public void Load() {
        // State = DebugModPlus.settings.ShowHitBoxes;
        Unload();
        SceneManager.activeSceneChanged += CreateHitboxRender;


        // ModHooks.ColliderCreateHook += UpdateHitboxRender;

        CreateHitboxRender();
    }

    public void Unload() {
        // State = DebugModPlus.settings.ShowHitBoxes;
        SceneManager.activeSceneChanged -= CreateHitboxRender;

        // ModHooks.ColliderCreateHook -= UpdateHitboxRender;
        DestroyHitboxRender();
    }

    private void CreateHitboxRender(Scene current, Scene next) => CreateHitboxRender();

    private void CreateHitboxRender() {
        DestroyHitboxRender();
        // if (GameManager.instance.IsGameplayScene()) hitboxRender = new GameObject().AddComponent<HitboxRender>();
        hitboxRender = new GameObject().AddComponent<HitboxRender>();
    }

    private void DestroyHitboxRender() {
        if (hitboxRender != null) {
            Object.Destroy(hitboxRender);
            hitboxRender = null;
        }
    }

    private void UpdateHitboxRender(GameObject go) {
        if (hitboxRender != null) hitboxRender.UpdateHitbox(go);
    }
}