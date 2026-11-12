using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public class OpeningTypeCatalogTests
{
    private const string CatalogPath = "Assets/Resources/OpeningTypeCatalog.asset";

    [Test]
    public void Catalog_HasNoDuplicateTypeKeys()
    {
        object catalog = LoadCatalog();
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (object item in GetItems(catalog))
        {
            Assert.That(item, Is.Not.Null);
            string typeKey = GetProperty<string>(item, "TypeKey");
            Assert.That(typeKey, Is.Not.Empty);
            Assert.That(keys.Add(typeKey), Is.True, $"Duplicate opening type key: {typeKey}");
        }
    }

    [Test]
    public void Catalog_ParametricProfilesAreSupportedByRuntimeModel()
    {
        object catalog = LoadCatalog();
        HashSet<string> supportedProfiles = GetSupportedParametricProfiles();

        foreach (object item in GetItems(catalog))
        {
            if (!GetProperty<bool>(item, "UseParametricModel"))
            {
                continue;
            }

            string typeKey = GetProperty<string>(item, "TypeKey");
            string profileKey = GetProperty<string>(item, "ParametricProfileKey");
            Assert.That(profileKey, Is.Not.Empty, $"{typeKey} uses a parametric model but has no profile key.");
            Assert.That(
                supportedProfiles.Contains(profileKey),
                Is.True,
                $"{typeKey} uses unsupported parametric profile '{profileKey}'.");
        }
    }

    [Test]
    public void Catalog_SizeConstraintsAreValid()
    {
        object catalog = LoadCatalog();

        foreach (object item in GetItems(catalog))
        {
            string typeKey = GetProperty<string>(item, "TypeKey");
            object referenceSize = GetProperty<object>(item, "ReferenceSize");
            object authoredSize = GetProperty<object>(item, "AuthoredSize");
            object minSize = GetProperty<object>(item, "MinSize");
            object maxSize = GetProperty<object>(item, "MaxSize");

            Assert.That(GetVectorAxis(referenceSize, "x"), Is.GreaterThan(0f), $"{typeKey} reference width must be positive.");
            Assert.That(GetVectorAxis(referenceSize, "y"), Is.GreaterThan(0f), $"{typeKey} reference height must be positive.");
            Assert.That(GetVectorAxis(referenceSize, "z"), Is.GreaterThan(0f), $"{typeKey} reference depth must be positive.");

            if (GetProperty<bool>(item, "UseParametricModel"))
            {
                Assert.That(GetVectorAxis(authoredSize, "x"), Is.GreaterThan(0f), $"{typeKey} authored width must be positive.");
                Assert.That(GetVectorAxis(authoredSize, "y"), Is.GreaterThan(0f), $"{typeKey} authored height must be positive.");
                Assert.That(GetVectorAxis(authoredSize, "z"), Is.GreaterThan(0f), $"{typeKey} authored depth must be positive.");
            }

            AssertMinMax(typeKey, "width", GetVectorAxis(minSize, "x"), GetVectorAxis(maxSize, "x"));
            AssertMinMax(typeKey, "height", GetVectorAxis(minSize, "y"), GetVectorAxis(maxSize, "y"));
            AssertMinMax(typeKey, "depth", GetVectorAxis(minSize, "z"), GetVectorAxis(maxSize, "z"));
        }
    }

    [Test]
    public void Catalog_S003_IsWindowLikePassThroughWindow()
    {
        object s003 = FindItem("S003");

        Assert.That(GetProperty<object>(s003, "OpeningType").ToString(), Is.EqualTo("Window"));
        Assert.That(GetProperty<object>(s003, "ModelLayoutKind").ToString(), Is.EqualTo("WindowLike"));
        Assert.That(GetProperty<object>(s003, "CollisionPolicy").ToString(), Is.EqualTo("PassThrough"));
        Assert.That(GetProperty<string>(s003, "ParametricProfileKey"), Is.EqualTo(GetProfileKey("WhiteTwoPanelWindow")));
    }

    [Test]
    public void Catalog_A002_IsApartmentVentWindowThatBlocksCharacter()
    {
        object a002 = FindItem("A002");

        Assert.That(GetProperty<object>(a002, "OpeningType").ToString(), Is.EqualTo("Window"));
        Assert.That(GetProperty<object>(a002, "ModelLayoutKind").ToString(), Is.EqualTo("WindowLike"));
        Assert.That(GetProperty<object>(a002, "CollisionPolicy").ToString(), Is.EqualTo("BlockCharacter"));
        Assert.That(GetProperty<string>(a002, "ParametricProfileKey"), Is.EqualTo(GetProfileKey("ApartmentVentWindow")));
    }

    [Test]
    public void Catalog_A003_IsClosedApartmentVentWindowType2ThatBlocksCharacter()
    {
        object a003 = FindItem("A003");

        Assert.That(GetProperty<object>(a003, "OpeningType").ToString(), Is.EqualTo("Window"));
        Assert.That(GetProperty<object>(a003, "ModelLayoutKind").ToString(), Is.EqualTo("WindowLike"));
        Assert.That(GetProperty<object>(a003, "CollisionPolicy").ToString(), Is.EqualTo("BlockCharacter"));
        Assert.That(GetProperty<string>(a003, "ParametricProfileKey"), Is.EqualTo(GetProfileKey("ApartmentVentWindowType2")));
    }

    private static object LoadCatalog()
    {
        Type catalogType = GetAssemblyType("OpeningTypeCatalog");
        object catalog = AssetDatabase.LoadAssetAtPath(CatalogPath, catalogType);
        Assert.That(catalog, Is.Not.Null, $"Opening type catalog not found at {CatalogPath}.");
        Assert.That(GetItems(catalog), Is.Not.Null);
        Assert.That(GetItems(catalog).Count, Is.GreaterThan(0));
        return catalog;
    }

    private static object FindItem(string typeKey)
    {
        object catalog = LoadCatalog();
        foreach (object item in GetItems(catalog))
        {
            if (item != null && string.Equals(GetProperty<string>(item, "TypeKey"), typeKey, StringComparison.Ordinal))
            {
                return item;
            }
        }

        Assert.Fail($"Opening type key not found: {typeKey}");
        return null;
    }

    private static HashSet<string> GetSupportedParametricProfiles()
    {
        Type profileKeysType = GetAssemblyType("ParametricOpeningProfileKeys");
        FieldInfo[] fields = profileKeysType.GetFields(BindingFlags.Public | BindingFlags.Static);
        HashSet<string> supportedProfiles = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i].FieldType == typeof(string))
            {
                supportedProfiles.Add((string)fields[i].GetValue(null));
            }
        }

        Assert.That(supportedProfiles.Count, Is.GreaterThan(0), "No supported parametric profiles were found.");
        return supportedProfiles;
    }

    private static string GetProfileKey(string fieldName)
    {
        Type profileKeysType = GetAssemblyType("ParametricOpeningProfileKeys");
        FieldInfo field = profileKeysType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(field, Is.Not.Null, $"Missing profile key field: {fieldName}");
        Assert.That(field.FieldType, Is.EqualTo(typeof(string)), $"Profile key field {fieldName} must be a string.");
        return (string)field.GetValue(null);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static IList GetItems(object catalog)
    {
        return GetProperty<IList>(catalog, "Items");
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"Missing property {propertyName} on {target.GetType().Name}.");
        return (T)property.GetValue(target);
    }

    private static float GetVectorAxis(object vector, string axisName)
    {
        FieldInfo field = vector.GetType().GetField(axisName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"Missing vector axis {axisName}.");
        return (float)field.GetValue(vector);
    }

    private static void AssertMinMax(string typeKey, string axisName, float min, float max)
    {
        Assert.That(min, Is.GreaterThanOrEqualTo(0f), $"{typeKey} minimum {axisName} must not be negative.");
        Assert.That(max, Is.GreaterThanOrEqualTo(0f), $"{typeKey} maximum {axisName} must not be negative.");

        if (min > 0f && max > 0f)
        {
            Assert.That(min, Is.LessThanOrEqualTo(max), $"{typeKey} minimum {axisName} must be <= maximum {axisName}.");
        }
    }
}
