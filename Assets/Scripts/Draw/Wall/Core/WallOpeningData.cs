using System;

[Serializable]
public class WallOpeningData
{
    public string id = Guid.NewGuid().ToString("N");
    public string openingType; // "Door", "Window"
    public float position; // 0.0(start) to 1.0(end)
    public float width;
    public float height;
    public float bottom;

    // ... Export codes or other metadata ...
}