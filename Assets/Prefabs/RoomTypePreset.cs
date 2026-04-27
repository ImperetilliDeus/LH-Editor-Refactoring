using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomType", menuName = "CustomPreset/RoomType")]
public class RoomTypePreset : ScriptableObject
{
    public List<RoomType> presets;

    [Serializable]
    public class RoomType
    {
        public string name;
        public int code;
    }
}
