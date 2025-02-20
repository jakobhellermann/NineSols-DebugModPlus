// ReSharper disable once CheckNamespace
// ReSharper disable UnusedType.Global

namespace System.Runtime.CompilerServices;

// Enable `string property { get; init; }`
internal static class IsExternalInit {
}

// Enable `required string x`
public class RequiredMemberAttribute : Attribute;

public class CompilerFeatureRequiredAttribute : Attribute {
    public CompilerFeatureRequiredAttribute(string name) {
    }
}