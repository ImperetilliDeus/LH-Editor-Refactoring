using UnityEngine;

[CreateAssetMenu(fileName = "UiReferenceSettings", menuName = "LH/UI Reference Settings")]
public sealed class UiReferenceSettings : ScriptableObject
{
    [Header("Furniture Menu")]
    public string furnitureMenuRootName = "_FurnishMenu";
    public string furnitureButtonName = "_Button_EditFurnish";
    public string furnitureScrollViewName = "Scroll View";

    [Header("Select Material")]
    public string materialScrollViewName = "Scroll View";
    public string materialContentName = "Content";
    public string materialFloorButtonName = "_Left";
    public string materialCeilingButtonName = "_Right";

    [Header("Room Wall Authoring")]
    public string roomWallMenuRootName = "RoomSelectMenu";
    public string roomWallScrollViewName = "WallScrollView";
    public string roomWallToggleContainerName = "_LayerToggleContainer";
    public string roomWallToggleTemplateName = "_WallToggleTemplate";
    public string roomWallHeaderTextName = "_HeaderText";
    public string roomWallStatusTextName = "_StatusText";
    public string roomWallAutoAssignButtonName = "_AutoAssignButton";
    public string roomWallResetButtonName = "_ResetButton";
    public string roomWallApplyButtonName = "_ApplyButton";
}
