internal interface IWallToolContext
{
    bool IsHandleInputLocked();
    void ClearPreviewSnappedHandle();
    bool TryConsumeEditSelectionPress();
    void DeleteCurrentSelection();
    bool TryPrepareWallCreationStart();
    void SetWallCreationModeActive(bool value);
    bool IsPreviewWallEnabled();
    void EnsurePreviewWallState();
    void UpdatePreviewWallState();
    void CommitCurrentSegmentState();
    void ExitWallCreationModeState();
}
