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
    private string BackingDir => backingDir ??= ModDirs.DataDir(DebugModPlus.Instance, "Savestates");

    private string SavestatePath(string slot) => Path.Join(BackingDir, $"{slot}.json");

    public IEnumerable<SavestateInfo> List(int? slot = null) {
        var pattern = slot != null ? $"{slot}-*.json" : "*.json";
        return Directory.GetFiles(BackingDir, pattern)
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

    public void Delete(int slot) {
        foreach (var info in List(slot)) {
            File.Delete(info.path);
        }
    }

    public void Save(string name, Savestate savestate, int? slot = null) {
        if (slot is { } i) Delete(i);
        var fullName = slot != null ? $"{slot}-{name}" : name;

        var path = SavestatePath(fullName);
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

    public bool TryGetValue(string fullName, [NotNullWhen(true)] out Savestate? savestate) {
        var path = SavestatePath(fullName);
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