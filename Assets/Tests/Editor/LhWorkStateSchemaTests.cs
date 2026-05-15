using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateSchemaTests
{
    [Test]
    public void CreateEmpty_ReturnsCurrentVersionAndEmptyCollections()
    {
        Type stateType = GetAssemblyType("LhWorkStateDto");
        object state = stateType.GetMethod("CreateEmpty", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
        int currentVersion = GetCurrentVersion(stateType);

        Assert.That(state, Is.Not.Null);
        Assert.That(GetFieldValue<int>(state, "version"), Is.EqualTo(currentVersion));
        Assert.That(GetFieldValue<IList>(state, "walls"), Is.Not.Null.And.Empty);
        Assert.That(GetFieldValue<IList>(state, "rooms"), Is.Not.Null.And.Empty);
        Assert.That(GetFieldValue<IList>(state, "furniture"), Is.Not.Null.And.Empty);
    }

    [Test]
    public void IsSupportedVersion_ReturnsFalse_ForUnsupportedVersion()
    {
        Type stateType = GetAssemblyType("LhWorkStateDto");
        MethodInfo method = stateType.GetMethod("IsSupportedVersion", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        int currentVersion = GetCurrentVersion(stateType);

        Assert.That((bool)method.Invoke(null, new object[] { 0 }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { currentVersion + 1 }), Is.False);
    }

    [Test]
    public void VectorDto_RoundTripsUnityVector3()
    {
        Type vectorDtoType = GetAssemblyType("LhWorkVector3Dto");
        Vector3 value = new Vector3(1.25f, 2.5f, -3.75f);

        object dto = vectorDtoType.GetMethod("FromVector3", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { value });
        object roundTripped = vectorDtoType.GetMethod("ToVector3", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(dto, null);

        Assert.That(roundTripped, Is.EqualTo(value));
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static int GetCurrentVersion(Type stateType)
    {
        FieldInfo field = stateType.GetField("CurrentVersion", BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null);
        return (int)field.GetValue(null);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }
}
