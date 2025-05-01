using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DebugModPlus.Savestates;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NineSolsAPI.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DebugModPlus;

public class Savestate {
    public string? Scene;
    public Vector3? PlayerPosition;
    public string? LastTeleportId;
    public List<ComponentSnapshot>? MonobehaviourSnapshots;
    public List<GameObjectSnapshot>? GameObjectSnapshots;
    public List<MonsterLoveFsmSnapshot>? FsmSnapshots;
    public List<GeneralFsmSnapshot>? GeneralFsmSnapshots;
    public List<ReferenceFixups>? ReferenceFixups;
    public JObject? Flags;
    public UnityEngine.Random.State? RandomState;

    public void SerializeTo(StreamWriter writer) {
        JsonSerializer.Create(jsonSettings).Serialize(writer, this);
    }

    public static Savestate DeserializeFrom(StreamReader reader) {
        var jsonReader = new JsonTextReader(reader);
        return JsonSerializer.Create(jsonSettings).Deserialize<Savestate>(jsonReader) ??
               throw new Exception("Failed to deserialize savestate");
    }

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public static Savestate Deserialize(string data) => JsonConvert.DeserializeObject<Savestate>(data) ??
                                                        throw new Exception("Failed to deserialize savestate");

    private static JsonSerializerSettings jsonSettings = new() {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new ForceSerializeResolver {
            ForceSerializePropertiesOf = [typeof(UnityEngine.Random.State)],
        },
        Converters = [new Vector3Converter()],
    };

    private class ForceSerializeResolver : DefaultContractResolver {
        public List<Type> ForceSerializePropertiesOf = [];

        protected override List<MemberInfo> GetSerializableMembers(Type objectType) {
            if (ForceSerializePropertiesOf.Contains(objectType)) {
                return objectType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Cast<MemberInfo>()
                    .ToList();
            }

            return base.GetSerializableMembers(objectType);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
            var property = base.CreateProperty(member, MemberSerialization.Fields);

            return property;
        }
    }
}

public record GameObjectData(bool Active);

public class GameObjectSnapshot {
    public required string Path;
    public required GameObjectData Data;

    public static GameObjectSnapshot Of(GameObject go) => new() {
        Path = ObjectUtils.ObjectPath(go),
        Data = new GameObjectData(go.activeSelf),
    };

    public bool Restore() {
        var targetGo = ObjectUtils.LookupPath(Path);
        if (!targetGo) {
            Log.Error($"Savestate stored state on {Path}, which does not exist at load time");
            return false;
        }

        targetGo!.SetActive(Data.Active);

        return true;
    }
}

public class ComponentSnapshot {
    public required string Path;
    public required JToken Data;

    public static ComponentSnapshot Of(Component mb) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Data = SnapshotSerializer.Snapshot(mb),
    };

    public bool Restore() {
        var targetComponent = ObjectUtils.LookupObjectComponentPath(Path);
        if (!targetComponent) {
            Log.Error($"Savestate stored state on {Path}, which does not exist at load time");
            return false;
        }

        SnapshotSerializer.Populate(targetComponent!, Data);

        return true;
    }
}

public class GeneralFsmSnapshot {
    public required string Path;
    public required string CurrentState;

    public static GeneralFsmSnapshot Of(StateMachineOwner owner) => new() {
        Path = ObjectUtils.ObjectPath(owner.gameObject),
        CurrentState = owner.FsmContext.fsm.State.name,
    };
}

public class MonsterLoveFsmSnapshot {
    public required string Path;
    public required object CurrentState;

    public static MonsterLoveFsmSnapshot Of(IStateMachine machine) => new() {
        Path = ObjectUtils.ObjectPath(machine.Component.gameObject),
        CurrentState = machine.CurrentStateMap.stateObj,
    };
}

public record ReferenceFixupField(string Field, string? Reference);

public class ReferenceFixups {
    public required string Path;
    public required List<ReferenceFixupField> Fields;

    public static ReferenceFixups Of(MonoBehaviour mb, List<ReferenceFixupField> fixups) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Fields = fixups,
    };
}