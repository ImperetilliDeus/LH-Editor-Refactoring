using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class PolylineXZTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void TryGetSegmentLengths_ReturnsLengthsTotalAndMinimumRequiredTotal()
    {
        List<Vector3> points = new List<Vector3> { Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(4f, 0f, 0f) };
        List<float> lengths = new List<float> { 99f };

        bool ok = PolylineXZ.TryGetSegmentLengths(points, 0.5f, lengths, out float total, out float minimumRequired);

        Assert.That(ok, Is.True);
        Assert.That(lengths, Is.EqualTo(new[] { 1f, 3f }).Within(Tolerance));
        Assert.That(total, Is.EqualTo(4f).Within(Tolerance));
        // The 1/4 segment must stay >= 0.5, so the whole chain must stay >= 2.
        Assert.That(minimumRequired, Is.EqualTo(2f).Within(Tolerance));
    }

    [Test]
    public void TryGetSegmentLengths_FailsOnZeroLengthSegmentOrTooFewPoints()
    {
        List<float> lengths = new List<float>();
        List<Vector3> withDuplicate = new List<Vector3> { Vector3.zero, Vector3.zero, new Vector3(2f, 0f, 0f) };

        Assert.That(PolylineXZ.TryGetSegmentLengths(withDuplicate, 0.1f, lengths, out _, out _), Is.False);
        Assert.That(PolylineXZ.TryGetSegmentLengths(new List<Vector3> { Vector3.zero }, 0.1f, lengths, out _, out _), Is.False);
        Assert.That(PolylineXZ.TryGetSegmentLengths(null, 0.1f, lengths, out _, out _), Is.False);
    }

    [Test]
    public void TryRescaleKeepingEnd_KeepsEqualRatiosOnStraightChain()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            new Vector3(3f, 0f, 0f),
        };

        bool ok = PolylineXZ.TryRescaleKeepingEnd(points, new Vector3(-3f, 0f, 0f), 0.01f, 0f, new List<float>());

        Assert.That(ok, Is.True);
        AssertPoint(points[0], -3f, 0f, 0f);
        AssertPoint(points[1], -1f, 0f, 0f);
        AssertPoint(points[2], 1f, 0f, 0f);
        AssertPoint(points[3], 3f, 0f, 0f);
    }

    [Test]
    public void TryRescaleKeepingEnd_KeepsUnequalRatiosAndAlignsToNewDirection()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
        };

        // Rotate the chain around its fixed end: the new start is 8 units away along -Z from (4,0,8).
        bool ok = PolylineXZ.TryRescaleKeepingEnd(points, new Vector3(4f, 0f, 8f), 0.01f, 0f, new List<float>());

        Assert.That(ok, Is.True);
        AssertPoint(points[0], 4f, 0f, 8f);
        AssertPoint(points[1], 4f, 0f, 6f);
        AssertPoint(points[2], 4f, 0f, 0f);
    }

    [Test]
    public void TryRescaleKeepingEnd_PlacesEveryPointOnPlaneY()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
        };

        bool ok = PolylineXZ.TryRescaleKeepingEnd(points, new Vector3(-2f, 5f, 0f), 0.01f, 1.5f, new List<float>());

        Assert.That(ok, Is.True);
        for (int i = 0; i < points.Count; i++)
        {
            Assert.That(points[i].y, Is.EqualTo(1.5f).Within(Tolerance));
        }

        AssertPoint(points[1], 0f, 1.5f, 0f);
    }

    [Test]
    public void TryRescaleKeepingEnd_FailsWithoutChangingPointsWhenASegmentWouldBeTooShort()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
        };
        List<Vector3> original = new List<Vector3>(points);

        // Total 4 -> 1.5 would shrink the 1/4 segment to 0.375, below the 0.5 minimum (needs total >= 2).
        bool ok = PolylineXZ.TryRescaleKeepingEnd(points, new Vector3(2.5f, 0f, 0f), 0.5f, 0f, new List<float>());

        Assert.That(ok, Is.False);
        Assert.That(points, Is.EqualTo(original));
    }

    [Test]
    public void TryRescaleKeepingEnd_FailsWhenNewTotalIsNotLongerThanMinimum()
    {
        List<Vector3> points = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f) };

        Assert.That(PolylineXZ.TryRescaleKeepingEnd(points, new Vector3(3.5f, 0f, 0f), 0.5f, 0f, new List<float>()), Is.False);
    }

    [Test]
    public void PointAtDistanceToward_MeasuresInXZAndKeepsOriginY()
    {
        Vector3 point = PolylineXZ.PointAtDistanceToward(new Vector3(1f, 2f, 1f), new Vector3(1.3f, 9f, 1.4f), 2f);

        AssertPoint(point, 2.2f, 2f, 2.6f);
    }

    private static void AssertPoint(Vector3 actual, float x, float y, float z)
    {
        Assert.That(actual.x, Is.EqualTo(x).Within(Tolerance), "x");
        Assert.That(actual.y, Is.EqualTo(y).Within(Tolerance), "y");
        Assert.That(actual.z, Is.EqualTo(z).Within(Tolerance), "z");
    }
}
