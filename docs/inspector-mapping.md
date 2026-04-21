# Inspector Mapping

`Assets/Scenes/SampleScene.unity` 기준으로 자주 확인하는 Inspector 연결 요약입니다.

## `ModeManager`

- `initialMode`

## `ModeButtonBinder`

- `_Button_Default`
  `targetMode` -> `Default`
  `targetButton` -> same GameObject `Button`
- `_Button_Room`
  `targetMode` -> `RoomCreate`
  `targetButton` -> same GameObject `Button`
- `_Button_EditDetail`
  `targetMode` -> `DetailEdit`
  `targetButton` -> same GameObject `Button`
- `_Button_EditFurnish`
  `targetMode` -> `FurniturePlace`
  `targetButton` -> same GameObject `Button`

## `RoomManager`

- `wallRoot`
- `roomRoot`
- `roomMaterial`
- `roomSpawnLocalOffset`

참고:
- room floor 시각 오브젝트 높이는 `Room` 내부 규칙으로 world `y = 0.1`에 배치됩니다.
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
- handle 시각화는 `Default` 모드에서만 표시됩니다.
- 입력은 `IEditorInputProvider`로 주입됩니다.

## `SnapManager`

- `gridSnapModifier`
- `axisSnapModifier`
- `enableHandleSnap`
- `enableHandleSnapModifier`
- `handleSnapModifier`
- `enableHandleDragGridSnapModifier`
- `handleDragGridSnapModifier`

권장:
- 기본 설정은 `handleSnapModifier = Ctrl`
- modifier 판정은 `IEditorInputProvider` 경로를 사용합니다.

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

참고:
- 입력은 `IEditorInputProvider`로 통일되었습니다.

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
