internal interface IWallToolContext
{
    bool IsHandleInputLocked();
    void ClearPreviewSnappedHandle();
    bool TryConsumeIdleSelectionPress();
    bool TryPrepareWallCreationStart();
    void SetWallCreationModeActive(bool value);
    bool IsPreviewWallEnabled();
    void EnsurePreviewWallState();
    void UpdatePreviewWallState();
    void CommitCurrentSegmentState();
    void ExitWallCreationModeState();
}
