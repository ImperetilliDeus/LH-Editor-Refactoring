using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LhWorkStateDto
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<LhWorkWallDto> walls = new List<LhWorkWallDto>();
    public List<LhWorkRoomDto> rooms = new List<LhWorkRoomDto>();
    public List<LhWorkFurnitureDto> furniture = new List<LhWorkFurnitureDto>();

    public static LhWorkStateDto CreateEmpty()
    {
        return new LhWorkStateDto
        {
            version = CurrentVersion,
            walls = new List<LhWorkWallDto>(),
            rooms = new List<LhWorkRoomDto>(),
            furniture = new List<LhWorkFurnitureDto>(),
        };
    }

    public static bool IsSupportedVersion(int value)
    {
        return value == CurrentVersion;
    }
}

[Serializable]
public class LhWorkWallDto
{
    public string id = string.Empty;
    public string name = string.Empty;
    public LhWorkVector3Dto start;
    public LhWorkVector3Dto end;
    public float thickness;
    public float height;
    public float centerY;
    public int startVertexId;
    public int endVertexId;
    public bool suppressStartHandle;
    public bool suppressEndHandle;
    public bool startSplitPoint;
    public bool endSplitPoint;
    public List<LhWorkOpeningDto> openings = new List<LhWorkOpeningDto>();
}

[Serializable]
public class LhWorkOpeningDto
{
    public string type = string.Empty;
    public string doorTypeKey = string.Empty;
    public string windowTypeKey = string.Empty;
    public bool doorOpensRight;
    public bool doorVerticalFlip;
    public float centerDistance;
    public float width;
    public float height;
    public float depth;
    public float bottomY;
}

[Serializable]
public class LhWorkRoomDto
{
    public string name = string.Empty;
    public string roomTypeKey = string.Empty;
    public string roomCode = string.Empty;
    public string roomNativeCode = string.Empty;
    public string floorTextureCode = string.Empty;
    public string ceilingTextureCode = string.Empty;
    public bool isManualRoom;
    public LhWorkVector3Dto placementOffset;
    public List<LhWorkVector3Dto> boundaryVertices = new List<LhWorkVector3Dto>();
    public List<string> wallIds = new List<string>();
    public bool manualWallSelectionEnabled;
    public List<string> manualWallIds = new List<string>();
}

[Serializable]
public class LhWorkFurnitureDto
{
    public string catalogCode = string.Empty;
    public string exportCode = string.Empty;
    public string nativeCode = string.Empty;
    public string name = string.Empty;
    public LhWorkVector3Dto position;
    public LhWorkVector3Dto eulerAngles;
    public LhWorkVector3Dto localScale;
    public bool isPlaced;
    public string roomName = string.Empty;
}

[Serializable]
public struct LhWorkVector3Dto
{
    public float x;
    public float y;
    public float z;

    public static LhWorkVector3Dto FromVector3(Vector3 value)
    {
        return new LhWorkVector3Dto { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
