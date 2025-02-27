using JetBrains.Annotations;
using NineSolsAPI;

namespace DebugModPlus;

[PublicAPI]
public class DebugSave {
    public const int DebugSaveIndex = 100;

    public static void LoadDebugSave() {
        SaveManager.Instance.LoadSaveAtSlot(DebugSaveIndex);
        ApplicationUIGroupManager.Instance.ClearAll();
        RuntimeInitHandler.LoadCore();

        if (!GameVersions.IsVersion(GameVersions.SpeedrunPatch)) {
            typeof(GameConfig).GetMethod("InstantiateGameCore")!.Invoke(GameConfig.Instance, []);
        }
    }
}