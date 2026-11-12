using System;
using NUnit.Framework;

// Runtime scripts compile into Assembly-CSharp, which test asmdefs cannot reference directly.
public static class TestReflection
{
    public static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
