using System;
using JetBrains.Annotations;
using UnityEngine;

namespace DebugMod;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[MeansImplicitUse]
public class BindableMethod : Attribute {
    public string Name;
    public KeyCode[] DefaultKeybind;
}