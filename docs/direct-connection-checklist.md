# Direct Connection Checklist

자동 탐색 fallback에 기대기보다, 아래 참조와 바인딩은 Inspector에서 직접 확인하는 것을 기본으로 합니다.

## `ModeManager`

- `initialMode`

## `ModeButtonBinder`

- `_Button_Default` -> `targetMode = Default`
- `_Button_Room` -> `targetMode = RoomCreate`
- `_Button_EditDetail` -> `targetMode = DetailEdit`
- `_Button_EditFurnish` -> `targetMode = FurniturePlace`
- `modeManager`
- `targetButton`

## `RoomManager` 계열

### `RoomAuthoringPanelManager`

- `modeManager`
- `roomManager`
- `roomHandleManager`
- `topViewRenderManager`
- `roomTypeDropdown`
- `roomEditMenu`
- `roomAreaInputField`

### `RoomHandleManager`

- `mainCamera`
- `grid`
- `targetCanvas`
- `snapManager`
- `wallHandleManager`
- `roomManager`
- `modeManager`

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

## Wall Edit 계열

- `DrawManager`
- `HandleManager`
- `WallSelectionManager`
- `SnapManager`
- `WallPropertyInputManager`
- `WallOpeningPlacementManager`

다음 참조가 비어 있으면 최근 수정 동작이 깨질 수 있습니다.

- `HandleManager.snapManager`
- `HandleManager.roomManager`
- `WallPropertyInputManager.handleManager`
- `WallPropertyInputManager.wallSelectionManager`
- `TopViewRenderManager.handleManager`

참고:
- wall 계열 런타임 입력은 `IEditorInputProvider` / `UnityEditorInputProvider` 경로로 통일되었습니다.
- `ModeManager`는 더 이상 UI 버튼을 직접 찾거나 버튼 필드를 직렬화하지 않습니다.
