using System.Collections.Generic;
using System.Reflection;

namespace DebugModPlus.Utils;

public static class Extensions {
    public static IEnumerable<AttackSensor> AttackSensorsCompat(this MonsterBase monsterBase) {
        var field = typeof(MonsterBase).GetField("attackSensors", BindingFlags.Instance | BindingFlags.Public);
        if (field != null) {
            return (List<AttackSensor>)field.GetValue(monsterBase);
        } else {
            var f = typeof(MonsterBase).GetField("_attackSensors", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (AttackSensor[])f.GetValue(monsterBase);
        }
    }
}