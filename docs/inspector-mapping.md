# Inspector Mapping

`SampleScene` 기준으로 자주 확인하는 Inspector 연결 요약입니다.

## `ModeManager`

- `Default Mode Button` -> `_Button_Default`
- `Room Create Mode Button` -> `_Button_Room`
- `Detail Edit Mode Button` -> `_Button_EditDetail`
- `Door Insert Mode Button` -> 필요 시 연결
- `Window Insert Mode Button` -> 필요 시 연결
- `Initial Mode` -> 보통 `Default`

## `RoomManager` GameObject

### `RoomManager`

- room prefab/material 관련 필드가 있다면 기존 프로젝트 설정 유지

### `RoomAuthoringPanelManager`

- `modeManager`
- `roomManager`
- `roomHandleManager`
- `topViewRenderManager`
- `roomTypeDropdown`
- `roomEditMenu`
- `roomAreaInputField`
- `roomTypeLabelPrefab`

### `RoomHandleManager`

- `mainCamera`
- `grid`
- `targetCanvas`
- `snapManager`
- `wallHandleManager`
- `roomManager`
- `modeManager`
- `showHandlesOnlyForFocusedRoom` -> `true` 권장

### `RoomCreateManager`

- `mainCamera`
- `grid`
- `wallRoot`
- `roomManager`
- `snapManager`
- `wallHandleManager`
- `roomHandleManager`
- `modeManager`
- `undoRedoManager`

## `TopViewRenderManager`

- `roomManager`
- `roomAuthoringPanelManager`
- `wallOpeningPlacementManager`
- `modeManager`
- top view 색상 필드
  - 기본 floor 색
  - 선택 room 색
  - wall 색
  - virtual boundary 색

## `WallEditManager` 계열

- `DrawManager`, `HandleManager`, `WallSelectionManager`, `SnapManager` 사이 참조가 비어 있지 않은지 확인
- `HandleManager`의 handle canvas가 wall 선보다 위에 그려지는지 확인

## 권장 확인 순서

1. `RoomAuthoringPanelManager.roomHandleManager`
2. `RoomCreateManager.roomHandleManager`
3. `RoomCreateManager.undoRedoManager`
4. `TopViewRenderManager.roomAuthoringPanelManager`
5. `ModeManager`의 room 버튼 연결
