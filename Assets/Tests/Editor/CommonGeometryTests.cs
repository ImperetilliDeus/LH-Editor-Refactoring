using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CommonGeometryTests
{
    private static readonly List<Vector3> CounterClockwiseSquare = new List<Vector3>
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(4f, 0f, 0f),
        new Vector3(4f, 0f, 4f),
        new Vector3(0f, 0f, 4f),
    };

    // L-shape: the 2x2 notch at x in [2,4], z in [2,4] is outside.
    private static readonly List<Vector3> LShape = new List<Vector3>
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(4f, 0f, 0f),
        new Vector3(4f, 0f, 2f),
        new Vector3(2f, 0f, 2f),
        new Vector3(2f, 0f, 4f),
        new Vector3(0f, 0f, 4f),
    };

    [Test]
    public void SignedArea_IsPositiveForCounterClockwiseAndNegativeWhenReversed()
    {
        List<Vector3> reversed = new List<Vector3>(CounterClockwiseSquare);
        reversed.Reverse();

        Assert.That(PolygonXZ.SignedArea(CounterClockwiseSquare), Is.EqualTo(16f).Within(0.0001f));
        Assert.That(PolygonXZ.SignedArea(reversed), Is.EqualTo(-16f).Within(0.0001f));
        Assert.That(PolygonXZ.SignedArea(LShape), Is.EqualTo(12f).Within(0.0001f));
    }

    [Test]
    public void SignedArea_IgnoresHeightAndReturnsZeroForDegenerateInput()
    {
        List<Vector3> raised = new List<Vector3>();
        for (int i = 0; i < CounterClockwiseSquare.Count; i++)
        {
            raised.Add(CounterClockwiseSquare[i] + Vector3.up * (i * 3f));
        }

        Assert.That(PolygonXZ.SignedArea(raised), Is.EqualTo(16f).Within(0.0001f));
        Assert.That(PolygonXZ.SignedArea(null), Is.EqualTo(0f));
        Assert.That(PolygonXZ.SignedArea(new List<Vector3> { Vector3.zero, Vector3.right }), Is.EqualTo(0f));
    }

    [Test]
    public void ContainsPoint_HandlesConcavePolygon()
    {
        Assert.That(PolygonXZ.ContainsPoint(new Vector3(1f, 5f, 1f), LShape), Is.True);
        Assert.That(PolygonXZ.ContainsPoint(new Vector3(1f, 0f, 3f), LShape), Is.True);
        Assert.That(PolygonXZ.ContainsPoint(new Vector3(3f, 0f, 3f), LShape), Is.False);
        Assert.That(PolygonXZ.ContainsPoint(new Vector3(-1f, 0f, 1f), LShape), Is.False);
    }

    [Test]
    public void ContainsPoint_ReturnsFalseForFewerThanThreeVertices()
    {
        Assert.That(PolygonXZ.ContainsPoint(Vector3.zero, null), Is.False);
        Assert.That(PolygonXZ.ContainsPoint(Vector3.zero, new List<Vector3> { Vector3.zero, Vector3.right }), Is.False);
    }

    [Test]
    public void ContainsBoundsFootprint_RequiresCenterAndAllCornersInside()
    {
        Bounds inside = new Bounds(new Vector3(1f, 0.5f, 1f), new Vector3(1f, 1f, 1f));
        Bounds overlappingNotch = new Bounds(new Vector3(2f, 0.5f, 2f), new Vector3(1f, 1f, 1f));

        Assert.That(PolygonXZ.ContainsBoundsFootprint(inside, LShape), Is.True);
        Assert.That(PolygonXZ.ContainsBoundsFootprint(overlappingNotch, LShape), Is.False);
    }

    [Test]
    public void BoundsXZ_ContainsPointIgnoresHeightAndIncludesEdges()
    {
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(2f, 0.1f, 2f));

        Assert.That(BoundsXZ.ContainsPoint(bounds, new Vector3(0.5f, 100f, -0.5f)), Is.True);
        Assert.That(BoundsXZ.ContainsPoint(bounds, new Vector3(1f, 0f, 1f)), Is.True);
        Assert.That(BoundsXZ.ContainsPoint(bounds, new Vector3(1.01f, 0f, 0f)), Is.False);
    }

    [Test]
    public void BoundsXZ_GetFootprintPointsReturnsCenterThenCornersAtCenterHeight()
    {
        Bounds bounds = new Bounds(new Vector3(1f, 2f, 3f), new Vector3(2f, 4f, 6f));

        Vector3[] points = BoundsXZ.GetFootprintPoints(bounds);

        Assert.That(points, Is.EqualTo(new[]
        {
            new Vector3(1f, 2f, 3f),
            new Vector3(0f, 2f, 0f),
            new Vector3(0f, 2f, 6f),
            new Vector3(2f, 2f, 0f),
            new Vector3(2f, 2f, 6f),
        }));
    }
}
