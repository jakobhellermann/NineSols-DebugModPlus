using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using NineSolsAPI.Utils;

namespace DebugModPlus.Modules;

class SavestateStore {
    private string? backingDir;
    private string BackingDir => backingDir ??= ModDirs.DataDir(DebugModPlus.Instance, "Savestates");

    private string SavestatePath(string slot) {
        return Path.Join(BackingDir, $"{slot}.json");
    }

    public void Save(string slot, Savestate savestate) {
        var path = SavestatePath(slot);
        try {
            var sw = Stopwatch.StartNew();
            using var file = File.CreateText(path);
            savestate.SerializeTo(file);
            Log.Info($"Saving state took {sw.ElapsedMilliseconds}ms");
        } catch (Exception) {
            File.Delete(path);
            throw;
        }
    }

    public bool TryGetValue(string slot, [NotNullWhen(true)] out Savestate? savestate) {
        var path = SavestatePath(slot);
        try {
            var sw = Stopwatch.StartNew();
            using var reader = File.OpenText(path);
            savestate = Savestate.DeserializeFrom(reader);
            Log.Info($"- Reading state from disk {sw.ElapsedMilliseconds}ms");
            return true;
        } catch (FileNotFoundException) {
            savestate = null;
            return false;
        }
    }
}