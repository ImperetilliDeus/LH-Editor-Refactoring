# Inspector Mapping

`Assets/Scenes/SampleScene.unity` 기준으로 자주 확인하는 Inspector 연결 요약입니다.

## `ModeManager`

- `Default Mode Button` -> `_Button_Default`
- `Room Create Mode Button` -> `_Button_Room`
- `Detail Edit Mode Button` -> `_Button_EditDetail`
- 필요 시 door/window insert 버튼 연결

## `RoomManager`

- `wallRoot`
- `roomRoot`
- `roomMaterial`
- `roomSpawnLocalOffset`

참고:
- room floor 실제 시각 오브젝트 높이는 `Room` 내부 정책으로 월드 `y = 0.1`에 배치됩니다.
- `roomSpawnLocalOffset`은 room 루트 위치 기준값이며 floor 자체 높이와는 별개입니다.

## `HandleManager`

- `mainCamera`
- `grid`
- `wallRoot`
- `targetCanvas`
- `snapManager`
- `wallLengthDisplay`
- `undoRedoManager`
- `modeManager`
- `roomManager`

참고:
- handle 시각은 `Default` 모드에서만 활성화됩니다.
- wall 변경 후 3D end-cap 재계산도 이 매니저 refresh 경로에 묶여 있습니다.

## `SnapManager`

- `gridSnapModifier`
- `axisSnapModifier`
- `enableHandleSnap`
- `enableHandleSnapModifier`
- `handleSnapModifier`
- `enableHandleDragGridSnapModifier`
- `handleDragGridSnapModifier`

권장:
- 현재 기본 정책은 `handleSnapModifier = Ctrl`

## `WallSelectionManager`

- `mainCamera`
- `grid`
- `wallRoot`
- `drawManager`
- `handleManager`
- `snapManager`
- `wallLengthDisplay`
- `undoRedoManager`
- `modeManager`
- `wallOpeningPlacementManager`
- `roomManager`

## `WallPropertyInputManager`

- `wallSelectionManager`
- `wallRoot`
- `handleManager`
- `wallLengthDisplay`
- `undoRedoManager`
- `modeManager`
- `roomManager`
- `wallOpeningPlacementManager`
- `wallLengthInputField`
- `wallHeightInputField`
- `wallThicknessInputField`

참고:
- 속성 변경 직후 handle / room / top view 갱신이 여기서 같이 일어납니다.

## `TopViewRenderManager`

- `topViewCamera`
- `targetCanvas`
- `contentRoot`
- `wallRoot`
- `drawManager`
- `handleManager`
- `wallSelectionManager`
- `wallOpeningPlacementManager`
- `roomManager`
- `roomAuthoringPanelManager`
- `roomHandleManager`
- `modeManager`
