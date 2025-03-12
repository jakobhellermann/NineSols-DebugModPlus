using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using NineSolsAPI.Utils;

namespace DebugModPlus.Modules;

public record SavestateInfo(
    string path,
    int? index,
    string? name
) {
    public string FullName => $"{index}-{name}";
}

internal class SavestateStore {
    private string? backingDir;

    private string BackingDir =>
        backingDir ??= ModDirs.DataDir(DebugModPlus.Instance, "Savestates");

    private string LayerDir(string? layer) => Path.Join(BackingDir, layer);

    private string SavestatePath(string slot, string? layer) => Path.Join(LayerDir(layer), $"{slot}.json");

    public IEnumerable<SavestateInfo> List(int? slot = null, string? layer = null) {
        var pattern = slot != null ? $"{slot}-*.json" : "*.json";
        var dir = LayerDir(layer);
        if (!Directory.Exists(dir)) {
            return [];
        }

        return Directory.GetFiles(dir, pattern)
            .Select(path => {
                if (SplitOnce(Path.GetFileNameWithoutExtension(path), '-') is not var (ord, name)) {
                    return new SavestateInfo(path, null, "");
                }

                if (!int.TryParse(ord, out var i)) {
                    return new SavestateInfo(path, null, "");
                }

                return new SavestateInfo(path, i, name);
            });
    }

    public void Delete(int slot, string? layer) {
        foreach (var info in List(slot, layer)) {
            File.Delete(info.path);
        }
    }

    public void Save(string name, Savestate savestate, int? slot = null, string? layer = null) {
        if (slot is { } i) Delete(i, layer);
        var fullName = slot != null ? $"{slot}-{name}" : name;

        Directory.CreateDirectory(LayerDir(layer));
        var path = SavestatePath(fullName, layer);
        try {
            using var file = File.CreateText(path);
            savestate.SerializeTo(file);
        } catch (Exception) {
            File.Delete(path);
            throw;
        }
    }

    public static bool TryGetValue(SavestateInfo info, [NotNullWhen(true)] out Savestate? savestate) =>
        TryGetValueInner(info.path, out savestate);

    public bool TryGetValue(string fullName, [NotNullWhen(true)] out Savestate? savestate, string? layer = null) {
        var path = SavestatePath(fullName, layer);
        return TryGetValueInner(path, out savestate);
    }

    private static bool TryGetValueInner(string path, [NotNullWhen(true)] out Savestate? savestate) {
        try {
            using var reader = File.OpenText(path);
            savestate = Savestate.DeserializeFrom(reader);
            return true;
        } catch (FileNotFoundException) {
            savestate = null;
            return false;
        }
    }

    private static (string, string)? SplitOnce(string str, char sep) {
        var length = str.IndexOf(sep);
        if (length == -1) return null;

        var startIndex = length + 1;
        var str3 = str.Substring(startIndex, str.Length - startIndex);
        return (str[..length], str3);
    }
}