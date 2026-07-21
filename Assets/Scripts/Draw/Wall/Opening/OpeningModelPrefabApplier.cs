using UnityEngine;

internal static class OpeningModelPrefabApplier
{
    public static void Apply(WallOpening opening, OpeningTypeCatalogItem definition)
    {
        if (opening == null)
        {
            return;
        }

        if (definition == null || definition.ModelPrefab == null)
        {
            opening.ClearModelPrefab();
            return;
        }

        opening.ApplyModelPrefab(
            definition.ModelPrefab,
            definition.ModelLocalPosition,
            definition.ModelLocalEulerAngles,
            definition.ModelScaleMultiplier,
            new Vector3(opening.Depth, opening.Height, opening.Width),
            new Vector3(opening.Width, opening.Height, opening.Depth),
            definition.ReferenceSize,
            definition.FitDepth,
            definition.FitHeight,
            definition.FitWidth,
            definition.UseParametricModel,
            definition.ParametricProfileKey,
            definition.AuthoredSize,
            definition.UsesBlenderLocalAxes);
    }
}
