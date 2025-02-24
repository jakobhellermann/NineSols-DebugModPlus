using Newtonsoft.Json.Linq;

namespace DebugModPlus.Savestates;

public static class FlagLogic {
    public static void LoadFlags(JObject newFlags, GameFlagCollection allFlags) {
        foreach (var keyValuePair in allFlags.flagDict) {
            var (name, gameFlagBase2) = keyValuePair;

            if (newFlags[name] is not JObject newField) continue;

            foreach (var (key, flagField) in gameFlagBase2.fieldCaches) {
                var jValue = newField[key];
                if (jValue == null) continue;

                switch (flagField) {
                    case FlagFieldBool flagFieldBool:
                        flagFieldBool.CurrentValue = jValue.Value<bool>();
                        break;
                    case FlagFieldInt flagFieldInt:
                        flagFieldInt.CurrentValue = jValue.Value<int>();
                        break;
                    case FlagFieldString flagFieldString:
                        flagFieldString.CurrentValue = jValue.Value<string>();
                        break;
                    case FlagFieldFloat flagFieldFloat:
                        flagFieldFloat.CurrentValue = jValue.Value<float>();
                        break;
                    case FlagFieldLong flagFieldLong:
                        flagFieldLong.CurrentValue = jValue.Value<long>();
                        break;
                }
            }
        }
    }
}