public static class GeometryTolerance
{
    public const float MinimumWallLength = 0.01f;

    // Edges whose Z extent is at or below this are skipped by the XZ point-in-polygon test.
    public const float PolygonEdgeDeltaEpsilon = 0.000001f;

    // Vertex merge and collinearity threshold used when sanitizing polygon outlines.
    public const float PolygonVertexEpsilon = 0.0001f;
    public const float PolygonVertexEpsilonSqr = 0.00000001f;
}
