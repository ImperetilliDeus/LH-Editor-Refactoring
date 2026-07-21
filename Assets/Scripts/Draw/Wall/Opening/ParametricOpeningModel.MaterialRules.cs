using System;
using System.Collections.Generic;
using UnityEngine;

internal static class ParametricOpeningMaterialRules
{
    private const string WhiteWindowClearGlassMaterialPath = "OpeningMaterials/WhiteWindow_ClearGlass";

    private static readonly Dictionary<string, Material> SharedProfileMaterials = new Dictionary<string, Material>();

    public static bool ShouldApply(string activeProfileKey)
    {
        return IsDoorProfile(activeProfileKey) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.WhiteTwoPanelWindow, StringComparison.Ordinal);
    }

    public static Material ResolveMaterial(string partName, string activeProfileKey)
    {
        if (string.Equals(activeProfileKey, ParametricOpeningProfileKeys.WhiteTwoPanelWindow, StringComparison.Ordinal))
        {
            return ResolveWindowMaterial(partName);
        }

        if (IsDoorProfile(activeProfileKey))
        {
            return IsWhiteDoorProfile(activeProfileKey)
                ? ResolveWhiteDoorMaterial(partName)
                : ResolveEntranceDoorMaterial(partName);
        }

        return null;
    }

    private static Material ResolveWindowMaterial(string partName)
    {
        if (ContainsAny(partName, "Glass"))
        {
            return GetResourceProfileMaterial(WhiteWindowClearGlassMaterialPath) ??
                   GetTransparentProfileMaterial("WhiteWindow_ClearGlass", new Color(0.78f, 0.9f, 0.96f, 0.28f), 0.0f, 0.9f);
        }

        if (ContainsAny(partName, "Handle"))
        {
            return GetProfileMaterial("WhiteWindow_SatinMetal", new Color(0.68f, 0.69f, 0.66f, 1f), 0.35f, 0.45f);
        }

        return GetProfileMaterial("WhiteWindow_Frame", new Color(0.88f, 0.86f, 0.78f, 1f), 0.0f, 0.38f);
    }

    private static Material ResolveWhiteDoorMaterial(string partName)
    {
        if (ContainsAny(partName, "Handle", "Knob", "Hinge", "Threshold", "Guide", "Rail"))
        {
            return GetProfileMaterial("WhiteDoor_SatinMetal", new Color(0.68f, 0.69f, 0.66f, 1f), 0.35f, 0.42f);
        }

        if (ContainsAny(partName, "Seal", "RecessLine", "Trim", "Mullion"))
        {
            return GetProfileMaterial("WhiteDoor_LightSeal", new Color(0.72f, 0.73f, 0.69f, 1f), 0.0f, 0.38f);
        }

        if (ContainsAny(partName, "Frame"))
        {
            return GetProfileMaterial("WhiteDoor_Frame", new Color(0.91f, 0.90f, 0.84f, 1f), 0.0f, 0.36f);
        }

        return GetProfileMaterial("WhiteDoor_Panel", new Color(0.86f, 0.85f, 0.79f, 1f), 0.0f, 0.42f);
    }

    private static Material ResolveEntranceDoorMaterial(string partName)
    {
        if (ContainsAny(partName, "Keypad"))
        {
            return GetProfileMaterial("EntranceDoor_KeypadBlack", new Color(0.04f, 0.045f, 0.05f, 1f), 0.25f, 0.65f);
        }

        if (ContainsAny(partName, "Lock", "Handle", "Hinge", "DoorPlaque", "TopCloser", "Threshold"))
        {
            return GetProfileMaterial("EntranceDoor_BrushedMetal", new Color(0.63f, 0.66f, 0.66f, 1f), 0.65f, 0.42f);
        }

        if (ContainsAny(partName, "DoorSlab_MainPanel"))
        {
            return GetProfileMaterial("EntranceDoor_DarkMainPanel", new Color(0.31f, 0.31f, 0.30f, 1f), 0.15f, 0.48f);
        }

        if (ContainsAny(partName, "VerticalAccent_LeftPanel"))
        {
            return GetProfileMaterial("EntranceDoor_LightSidePanel", new Color(0.70f, 0.71f, 0.69f, 1f), 0.2f, 0.38f);
        }

        if (ContainsAny(partName, "Frame", "Seal", "VerticalTrim", "RecessLine"))
        {
            return GetProfileMaterial("EntranceDoor_DarkTrim", new Color(0.12f, 0.125f, 0.12f, 1f), 0.25f, 0.35f);
        }

        return GetProfileMaterial("EntranceDoor_DefaultGray", new Color(0.55f, 0.56f, 0.55f, 1f), 0.1f, 0.35f);
    }

    private static Material GetResourceProfileMaterial(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        if (SharedProfileMaterials.TryGetValue(resourcePath, out Material cachedMaterial) && cachedMaterial != null)
        {
            return cachedMaterial;
        }

        Material material = Resources.Load<Material>(resourcePath);
        if (material != null)
        {
            SharedProfileMaterials[resourcePath] = material;
        }

        return material;
    }

    private static Material GetProfileMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        if (SharedProfileMaterials.TryGetValue(materialName, out Material cachedMaterial) && cachedMaterial != null)
        {
            if (IsCompatibleProfileShader(cachedMaterial.shader))
            {
                return cachedMaterial;
            }

            SharedProfileMaterials.Remove(materialName);
        }

        Shader shader = FindSupportedProfileShader();
        if (shader == null)
        {
            Debug.LogWarning($"Parametric opening material '{materialName}' could not be created because no compatible shader was found.");
            return null;
        }

        Material material = new Material(shader)
        {
            name = materialName,
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }

        SharedProfileMaterials[materialName] = material;
        return material;
    }

    private static Shader FindSupportedProfileShader()
    {
        bool usesScriptableRenderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        string[] shaderNames = usesScriptableRenderPipeline
            ? new[]
            {
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
                "Sprites/Default",
                "Standard",
                "Unlit/Color",
            }
            : new[]
            {
                "Standard",
                "Unlit/Color",
                "Sprites/Default",
                "Universal Render Pipeline/Lit",
                "Universal Render Pipeline/Unlit",
            };

        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (IsCompatibleProfileShader(shader))
            {
                return shader;
            }
        }

        return null;
    }

    private static bool IsCompatibleProfileShader(Shader shader)
    {
        if (shader == null || !shader.isSupported)
        {
            return false;
        }

        bool usesScriptableRenderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        bool isUrpShader = shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
        return usesScriptableRenderPipeline || !isUrpShader;
    }

    private static Material GetTransparentProfileMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        Material material = GetProfileMaterial(materialName, color, metallic, smoothness);
        if (material == null)
        {
            return null;
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private static bool IsDoorProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, ParametricOpeningProfileKeys.EntranceDoorLockset, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.SingleEntranceDoor, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.EntranceDoorSidePanel, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.WhiteGlassFrameDoor, StringComparison.Ordinal) ||
               (!string.IsNullOrEmpty(activeProfileKey) && activeProfileKey.Contains("DOOR"));
    }

    private static bool IsWhiteDoorProfile(string activeProfileKey)
    {
        return !string.IsNullOrWhiteSpace(activeProfileKey) &&
               activeProfileKey.IndexOf("WHITE_", StringComparison.Ordinal) >= 0;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        if (string.IsNullOrEmpty(value) || patterns == null)
        {
            return false;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            if (!string.IsNullOrEmpty(patterns[i]) &&
                value.IndexOf(patterns[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

public sealed partial class ParametricOpeningModel
{
    private void ApplyProfileMaterials(string activeProfileKey)
    {
        if (!ParametricOpeningMaterialRules.ShouldApply(activeProfileKey))
        {
            return;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            Transform partTransform = parts[i].Transform;
            if (partTransform == null)
            {
                continue;
            }

            Renderer renderer = partTransform.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            Material material = ParametricOpeningMaterialRules.ResolveMaterial(partTransform.name, activeProfileKey);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
