using System.IO;
using System.Linq;
using DebugModPlus.Savestates;
using NineSolsAPI;

namespace DebugModPlus.Interop;

public static class DebugModPlusInterop {
    public static Savestate CreateSavestate(int filter) => SavestateLogic.Create((SavestateFilter)filter);

    public static bool LoadSavestate(Savestate savestate) {
        var task = SavestateLogic.Load((Savestate)savestate, SavestateLoadMode.None);
        return task.IsCompleted;
    }

    public static Savestate CreateSavestateDisk(string name, string? layer, int filter) {
        var savestate = SavestateLogic.Create((SavestateFilter)filter);
        DebugModPlus.Instance.SavestateModule.Savestates.Save(name, savestate, null, layer);
        return savestate;
    }

    public static bool LoadSavestateDisk(string name, string? layer) {
        if (DebugModPlus.Instance.SavestateModule.Savestates.TryGetValue(name, out var savestate, layer)) {
            var task = SavestateLogic.Load(savestate, SavestateLoadMode.None);
            if (!task.IsCompleted) {
                ToastManager.Toast("Did not load savestate instantly");
            }

            return true;
        }

        return false;
    }

    public static string[] ListSavestates(string? layer) {
        return DebugModPlus.Instance.SavestateModule.Savestates.List(null, layer)
            .Select(x => Path.GetFileNameWithoutExtension(x.path))
            .ToArray();
    }
}