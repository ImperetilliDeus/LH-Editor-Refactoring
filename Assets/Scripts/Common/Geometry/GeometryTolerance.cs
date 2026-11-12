public static class GeometryTolerance
{
    public const float MinimumWallLength = 0.01f;

    // Edges whose Z extent is at or below this are skipped by the XZ point-in-polygon test.
    public const float PolygonEdgeDeltaEpsilon = 0.000001f;
}
