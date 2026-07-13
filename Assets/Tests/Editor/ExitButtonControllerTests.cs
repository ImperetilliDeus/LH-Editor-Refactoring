using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExitButtonControllerTests
{
    private GameObject buttonObject;

    [TearDown]
    public void TearDown()
    {
        ResetQuitApplication();

        if (buttonObject != null)
        {
            UnityEngine.Object.DestroyImmediate(buttonObject);
        }
    }

    [Test]
    public void ButtonClick_RequestsApplicationQuit()
    {
        int quitRequestCount = 0;
        buttonObject = new GameObject("ExitButton");
        Button button = buttonObject.AddComponent<Button>();
        Type controllerType = GetControllerType();
        FieldInfo quitApplicationField = controllerType.GetField("QuitApplication", BindingFlags.Public | BindingFlags.Static);
        Assert.That(quitApplicationField, Is.Not.Null);
        quitApplicationField.SetValue(null, (Action)(() => quitRequestCount++));

        buttonObject.AddComponent(controllerType);
        button.onClick.Invoke();

        Assert.That(quitRequestCount, Is.EqualTo(1));
    }

    private static Type GetControllerType()
    {
        Type type = Type.GetType("ExitButtonController, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, "Expected Assembly-CSharp type ExitButtonController.");
        return type;
    }

    private static void ResetQuitApplication()
    {
        Type controllerType = Type.GetType("ExitButtonController, Assembly-CSharp");
        if (controllerType == null)
        {
            return;
        }

        FieldInfo quitApplicationField = controllerType.GetField("QuitApplication", BindingFlags.Public | BindingFlags.Static);
        MethodInfo defaultQuitMethod = controllerType.GetMethod("DefaultQuitApplication", BindingFlags.Public | BindingFlags.Static);
        if (quitApplicationField == null || defaultQuitMethod == null)
        {
            return;
        }

        Delegate defaultQuitDelegate = Delegate.CreateDelegate(quitApplicationField.FieldType, defaultQuitMethod);
        quitApplicationField.SetValue(null, defaultQuitDelegate);
    }
}
