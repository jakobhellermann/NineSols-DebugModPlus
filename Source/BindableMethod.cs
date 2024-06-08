using System;
using JetBrains.Annotations;

namespace DebugMod.Source;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[MeansImplicitUse]
public class BindableMethod : Attribute {
    public string Name;
}