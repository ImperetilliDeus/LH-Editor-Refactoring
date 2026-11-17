using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class WallPropertyInputManager : MonoBehaviour
{
    private const float MinimumWallLength = GeometryTolerance.MinimumWallLength;

    private enum LengthAnchorMode
    {
        LeftFixed,
        RightFixed,
    }

    [Header("References")]
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;

    [Header("Inputs")]
    [SerializeField] private InputField wallLengthInputField;
    [SerializeField] private InputField wallHeightInputField;
    [SerializeField] private InputField wallThicknessInputField;
    [SerializeField] private GameObject addOpeningsTarget;

    [Header("Length Anchor")]
    [SerializeField] private Button leftLengthAnchorButton;
    [SerializeField] private Button rightLengthAnchorButton;
    [SerializeField] private LengthAnchorMode lengthAnchorMode = LengthAnchorMode.LeftFixed;

    private readonly List<Wall> resizeAffectedWalls = new List<Wall>();
    private readonly WallPropertyPresentationController presentationController = new WallPropertyPresentationController();
    private readonly WallPropertySelectionService selectionService = new WallPropertySelectionService();
    private readonly WallPropertyMutationService mutationService = new WallPropertyMutationService();
    private bool suppressInputCallback;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        BindAnchorButtons();
        EnsureWallRoot();
        BindStateEvents();
        UpdateInputFieldValues(true);
    }

    private void OnDestroy()
    {
        UnbindStateEvents();
        UnbindAnchorButtons();
    }

    public void ApplySelectedWallLengthFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallLengthInputField != null)
        {
            inputText = wallLengthInputField.text;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetLengthMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetLengthUnits = MeasurementUnits.MillimetersToUnits(targetLengthMillimeters);
        if (targetLengthUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerLengthFromSelectedWall(selectedWallComponent, targetLengthUnits))
        {
            return;
        }

        mutationService.ApplyWallLength(
            selectedWallComponent,
            targetLengthUnits,
            lengthAnchorMode == LengthAnchorMode.LeftFixed,
            MinimumWallLength,
            wallLengthDisplay,
            resizeAffectedWalls,
            CollectWallsSharingVertex,
            RefreshAfterEdit);
    }

    public void ApplySelectedWallLengthFromCurrentInput()
    {
        if (wallLengthInputField == null)
        {
            return;
        }

        ApplySelectedWallLengthFromInput(wallLengthInputField.text);
    }

    public void SetLeftLengthAnchor()
    {
        lengthAnchorMode = LengthAnchorMode.LeftFixed;
        UpdateAnchorButtonState();
    }

    public void SetRightLengthAnchor()
    {
        lengthAnchorMode = LengthAnchorMode.RightFixed;
        UpdateAnchorButtonState();
    }

    public void ApplySelectedWallHeightFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallHeightInputField != null)
        {
            inputText = wallHeightInputField.text;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetHeightMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetHeightUnits = MeasurementUnits.MillimetersToUnits(targetHeightMillimeters);
        if (targetHeightUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerHeightFromSelectedWall(selectedWallComponent, targetHeightUnits))
        {
            return;
        }

        mutationService.ApplyWallHeight(
            selectedWall,
            selectedWallComponent,
            targetHeightUnits,
            wallLengthDisplay,
            RefreshAfterEdit);
    }

    public void ApplySelectedWallHeightFromCurrentInput()
    {
        if (wallHeightInputField == null)
        {
            return;
        }

        ApplySelectedWallHeightFromInput(wallHeightInputField.text);
    }

    public void ApplySelectedWallThicknessFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallThicknessInputField != null)
        {
            inputText = wallThicknessInputField.text;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetThicknessMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetThicknessUnits = MeasurementUnits.MillimetersToUnits(targetThicknessMillimeters);
        if (targetThicknessUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerThicknessFromSelectedWall(selectedWallComponent, targetThicknessUnits))
        {
            return;
        }

        mutationService.ApplyWallThickness(
            selectedWall,
            selectedWallComponent,
            targetThicknessUnits,
            wallLengthDisplay,
            RefreshAfterEdit);
    }

    public void ApplySelectedWallThicknessFromCurrentInput()
    {
        if (wallThicknessInputField == null)
        {
            return;
        }

        ApplySelectedWallThicknessFromInput(wallThicknessInputField.text);
    }

    private void RefreshAfterEdit()
    {
        mutationService.RefreshAfterEdit(
            GetSelectedWall(),
            handleManager,
            wallSelectionManager,
            MarkTopViewDirty,
            UpdateInputFieldValues);
    }

    private bool TryParseMillimeterInput(string inputText, out float millimeters)
    {
        millimeters = 0f;

        if (suppressInputCallback || string.IsNullOrWhiteSpace(inputText))
        {
            return false;
        }

        if (!UnitDisplayUtility.TryParseMillimeters(inputText, out millimeters))
        {
            return false;
        }

        return millimeters > 0f;
    }

    private void CollectWallsSharingVertex(int vertexId, List<Wall> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (wallRoot == null || vertexId <= 0)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, result);
        for (int i = result.Count - 1; i >= 0; i--)
        {
            Wall wall = result[i];
            if (wall == null || !wall.ContainsVertexId(vertexId))
            {
                result.RemoveAt(i);
            }
        }
    }

    private GameObject GetSelectedWall()
    {
        return selectionService.GetSelectedWall(wallSelectionManager);
    }

    private float GetDisplayedLengthUnits(Wall wall)
    {
        if (wall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallLength : wall.Data.GetLength();
    }

    private float GetDisplayedHeightUnits(GameObject selectedWall)
    {
        if (selectedWall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = selectedWall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallHeight : selectedWall.transform.localScale.y;
    }

    private float GetDisplayedThicknessUnits(GameObject selectedWall)
    {
        if (selectedWall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = selectedWall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallThickness : selectedWall.transform.localScale.x;
    }

    private bool TryApplyContainerLengthFromSelectedWall(Wall selectedWallComponent, float targetLengthUnits)
    {
        return mutationService.TryApplyContainerLengthFromSelectedWall(
            selectedWallComponent,
            targetLengthUnits,
            lengthAnchorMode == LengthAnchorMode.LeftFixed,
            MinimumWallLength,
            wallLengthDisplay,
            wallOpeningPlacementManager,
            handleManager,
            MarkTopViewDirty,
            UpdateInputFieldValues,
            resizeAffectedWalls,
            CollectWallsSharingVertex);
    }

    private bool TryApplyContainerHeightFromSelectedWall(Wall selectedWallComponent, float targetHeightUnits)
    {
        return mutationService.TryApplyContainerHeightFromSelectedWall(
            selectedWallComponent,
            targetHeightUnits,
            wallOpeningPlacementManager,
            handleManager,
            MarkTopViewDirty,
            UpdateInputFieldValues);
    }

    private bool TryApplyContainerThicknessFromSelectedWall(Wall selectedWallComponent, float targetThicknessUnits)
    {
        return mutationService.TryApplyContainerThicknessFromSelectedWall(
            selectedWallComponent,
            targetThicknessUnits,
            wallOpeningPlacementManager,
            handleManager,
            MarkTopViewDirty,
            UpdateInputFieldValues);
    }

    private void EnsureWallRoot()
    {
        LayerUtility.ResolveWallRoot(ref wallRoot, true);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
    }

    private void MarkTopViewDirty()
    {
        EditorVisualEvents.RequestTopViewRefresh();
    }
}
