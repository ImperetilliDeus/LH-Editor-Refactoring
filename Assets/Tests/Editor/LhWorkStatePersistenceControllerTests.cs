using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStatePersistenceControllerTests
{
    private GameObject wallRoot;
    private GameObject controllerObject;

    [SetUp]
    public void SetUp()
    {
        wallRoot = new GameObject("Walls");
        controllerObject = new GameObject("WorkStateController");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(controllerObject);
        UnityEngine.Object.DestroyImmediate(wallRoot);
    }

    [Test]
    public void SaveToPath_WritesVersionedJsonFile()
    {
        string path = Path.Combine(Application.temporaryCachePath, "lh-work-state-test.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        try
        {
            Component controller = controllerObject.AddComponent(GetAssemblyType("LhWorkStatePersistenceController"));
            controller.GetType()
                .GetMethod("SetReferencesForTests", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(controller, new object[] { wallRoot.transform, null, null, null });

            controller.GetType()
                .GetMethod("SaveToPath", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(controller, new object[] { path });

            Assert.That(File.Exists(path), Is.True);
            string json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"version\""));
            Assert.That(json, Does.Contain("\"walls\""));
            Assert.That(json, Does.Contain("\"rooms\""));
            Assert.That(json, Does.Contain("\"furniture\""));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void ResolveDefaultPath_UsesWorkStateExtension()
    {
        Component controller = controllerObject.AddComponent(GetAssemblyType("LhWorkStatePersistenceController"));

        string path = (string)controller.GetType()
            .GetMethod("ResolveDefaultPath", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(controller, null);

        Assert.That(Path.GetExtension(path), Is.EqualTo(".lhscene"));
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
