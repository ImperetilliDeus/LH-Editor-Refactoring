using System;
using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private sealed partial class PartState
    {
        public readonly Transform Transform;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly Vector3 Size;
        public readonly bool StretchX;
        public readonly bool StretchY;
        public readonly bool StretchZ;
        public readonly bool UseAdditiveStretchX;
        public readonly bool UseAdditiveStretchY;
        public readonly bool UseAdditiveStretchZ;
        public readonly bool IsRailingVerticalBar;
        public readonly bool OriginalActive;
        public readonly AxisAnchor AnchorX;
        public readonly AxisAnchor AnchorY;
        public readonly AxisAnchor AnchorZ;

        public PartState(
            Transform transform,
            Vector3 referenceSize,
            string activeProfileKey,
            bool activeUsesBlenderLocalAxes)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
            Size = ResolveAuthoredPartSize(transform, referenceSize, activeProfileKey);
            StretchX = ParametricOpeningPartTransformRules.ShouldStretchX(transform.name, activeProfileKey);
            StretchY = ParametricOpeningPartTransformRules.ShouldStretchY(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            StretchZ = ParametricOpeningPartTransformRules.ShouldStretchZ(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            bool isDoorProfile = IsDoorProfile(activeProfileKey);
            UseAdditiveStretchX = isDoorProfile && StretchX;
            UseAdditiveStretchY = isDoorProfile && StretchY;
            UseAdditiveStretchZ = isDoorProfile && StretchZ;
            IsRailingVerticalBar = transform.name.Contains("Railing_Vertical_Bar");
            OriginalActive = transform.gameObject.activeSelf;
            AnchorX = ParametricOpeningPartTransformRules.ResolveXAnchor(transform.name, activeProfileKey);
            AnchorY = ParametricOpeningPartTransformRules.ResolveYAnchor(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            AnchorZ = ParametricOpeningPartTransformRules.ResolveZAnchor(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
        }

        private static Vector3 ResolveAuthoredPartSize(Transform transform, Vector3 referenceSize, string activeProfileKey)
        {
            if (IsDoorProfile(activeProfileKey))
            {
                if (IsSingleEntranceDoorProfile(activeProfileKey))
                {
                    return EntranceDoorOpeningPartSizes.ResolveSingleEntranceDoor(transform.name, referenceSize, MinimumSize);
                }

                if (IsEntranceDoorSidePanelProfile(activeProfileKey))
                {
                    return EntranceDoorOpeningPartSizes.ResolveEntranceDoorSidePanel(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteOneSlidingDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveOneSlidingDoor(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteSwingDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveSwingDoor(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteGlassFrameDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveGlassFrameDoor(transform.name, referenceSize, MinimumSize);
                }

                return EntranceDoorOpeningPartSizes.ResolveEntranceDoorLockset(transform.name, referenceSize, MinimumSize);
            }

            if (string.Equals(activeProfileKey, WhiteTwoPanelWindowProfile, StringComparison.Ordinal))
            {
                return WhiteWindowOpeningPartSizes.ResolveTwoPanelWindow(transform.name, referenceSize, MinimumSize);
            }

            return CalculateApproximateLocalSize(transform);
        }

        private static Vector3 CalculateApproximateLocalSize(Transform transform)
        {
            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
                return new Vector3(
                    Mathf.Abs(meshSize.x * transform.localScale.x),
                    Mathf.Abs(meshSize.y * transform.localScale.y),
                    Mathf.Abs(meshSize.z * transform.localScale.z));
            }

            return Vector3.one * MinimumSize;
        }
    }
}
