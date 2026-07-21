using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplyWhiteOneSlidingDoorLayout(Vector3 targetSize)
    {
        WhiteDoorOpeningLayout.ApplyOneSlidingDoor(targetSize, MinimumSize, ApplyKnownPart);
    }

    private void ApplyWhiteSwingDoorLayout(Vector3 targetSize)
    {
        WhiteDoorOpeningLayout.ApplySwingDoor(targetSize, MinimumSize, ApplyKnownPart);
    }

    private void ApplyWhiteGlassFrameDoorLayout(Vector3 targetSize)
    {
        WhiteDoorOpeningLayout.ApplyGlassFrameDoor(targetSize, MinimumSize, ApplyKnownPart);
    }
}
