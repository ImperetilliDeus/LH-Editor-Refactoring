using UnityEngine;

public static class WallTextureTilingUtility
{
    private const float WallUnitToTextureMeter = 0.01f;
    private const string BaseMapScaleOffsetProperty = "_BaseMap_ST";
    private const string MainTextureScaleOffsetProperty = "_MainTex_ST";

    private static readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    public static void ApplyWallTiling(MeshRenderer renderer, Vector3 wallScale)
    {
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.sharedMaterial;
        if (material == null)
        {
            renderer.SetPropertyBlock(null);
            return;
        }

        Vector2 materialScale = GetTextureScale(material);
        Vector2 materialOffset = GetTextureOffset(material);
        float lengthRepeats = Mathf.Max(WallUnitToTextureMeter, Mathf.Abs(wallScale.z) * WallUnitToTextureMeter);
        float heightRepeats = Mathf.Max(WallUnitToTextureMeter, Mathf.Abs(wallScale.y) * WallUnitToTextureMeter);
        Vector4 scaleOffset = new Vector4(
            materialScale.x * lengthRepeats,
            materialScale.y * heightRepeats,
            materialOffset.x,
            materialOffset.y);

        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(BaseMapScaleOffsetProperty, scaleOffset);
        propertyBlock.SetVector(MainTextureScaleOffsetProperty, scaleOffset);
        renderer.SetPropertyBlock(propertyBlock);
    }

    public static void ApplyCapTiling(MeshRenderer renderer, Vector3 capWorldScale)
    {
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.sharedMaterial;
        if (material == null)
        {
            renderer.SetPropertyBlock(null);
            return;
        }

        Vector2 materialScale = GetTextureScale(material);
        Vector2 materialOffset = GetTextureOffset(material);
        float widthRepeats = Mathf.Max(WallUnitToTextureMeter, Mathf.Abs(capWorldScale.x) * WallUnitToTextureMeter);
        float heightRepeats = Mathf.Max(WallUnitToTextureMeter, Mathf.Abs(capWorldScale.y) * WallUnitToTextureMeter);
        Vector4 scaleOffset = new Vector4(
            materialScale.x * widthRepeats,
            materialScale.y * heightRepeats,
            materialOffset.x,
            materialOffset.y);

        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetVector(BaseMapScaleOffsetProperty, scaleOffset);
        propertyBlock.SetVector(MainTextureScaleOffsetProperty, scaleOffset);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private static Vector2 GetTextureScale(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            return material.GetTextureScale("_BaseMap");
        }

        return material.HasProperty("_MainTex")
            ? material.GetTextureScale("_MainTex")
            : Vector2.one;
    }

    private static Vector2 GetTextureOffset(Material material)
    {
        if (material.HasProperty("_BaseMap"))
        {
            return material.GetTextureOffset("_BaseMap");
        }

        return material.HasProperty("_MainTex")
            ? material.GetTextureOffset("_MainTex")
            : Vector2.zero;
    }
}
