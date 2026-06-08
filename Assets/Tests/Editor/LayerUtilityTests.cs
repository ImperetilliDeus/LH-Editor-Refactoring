using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LayerUtilityTests
{
    private GameObject registryObject;
    private GameObject wallRootObject;

    [TearDown]
    public void TearDown()
    {
        DestroyObject(registryObject);
        DestroyObject(wallRootObject);
    }

    [Test]
    public void FindWallRoot_UsesSceneReferenceRegistryWallRoot()
    {
        wallRootObject = new GameObject("ConfigurableWallRoot");
        Component registry = CreateRegistry();
        SetPrivateField(registry, "wallRoot", wallRootObject.transform);

        Transform result = InvokeFindWallRoot();

        Assert.That(result, Is.SameAs(wallRootObject.transform));
    }

    [Test]
    public void FindWallRoot_UsesWallRootMarkerWhenRegistryIsEmpty()
    {
        wallRootObject = new GameObject("AnyRootName");
        wallRootObject.AddComponent(GetType("WallRootMarker"));

        Transform result = InvokeFindWallRoot();

        Assert.That(result, Is.SameAs(wallRootObject.transform));
    }

    [Test]
    public void FindWallRoot_DoesNotResolveByLegacyNameWithoutMarkerOrRegistry()
    {
        wallRootObject = new GameObject("Walls");

        Transform result = InvokeFindWallRoot();

        Assert.That(result, Is.Null);
    }

    [Test]
    public void ResolveWallRoot_CreatesMarkedRootWhenRequested()
    {
        Transform wallRoot = null;

        wallRoot = InvokeResolveWallRoot(wallRoot, true, true);

        Assert.That(wallRoot, Is.Not.Null);
        Assert.That(wallRoot.GetComponent(GetType("WallRootMarker")), Is.Not.Null);
        wallRootObject = wallRoot.gameObject;
    }

    private Component CreateRegistry()
    {
        registryObject = new GameObject("SceneReferenceRegistry");
        return registryObject.AddComponent(GetType("SceneReferenceRegistry"));
    }

    private static Transform InvokeFindWallRoot()
    {
        MethodInfo method = GetType("LayerUtility").GetMethod(
            "FindWallRoot",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(bool) },
            null);
        Assert.That(method, Is.Not.Null);
        return (Transform)method.Invoke(null, new object[] { true });
    }

    private static Transform InvokeResolveWallRoot(Transform wallRoot, bool includeInactive, bool createIfMissing)
    {
        MethodInfo method = GetType("LayerUtility").GetMethod(
            "ResolveWallRoot",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Transform).MakeByRefType(), typeof(bool), typeof(bool) },
            null);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { wallRoot, includeInactive, createIfMissing };
        method.Invoke(null, arguments);
        return (Transform)arguments[0];
    }

    private static System.Type GetType(string typeName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null);
        return type;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void DestroyObject(Object target)
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }
}
