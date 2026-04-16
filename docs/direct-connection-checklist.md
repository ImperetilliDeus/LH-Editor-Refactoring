# Direct Connection Checklist

자동 탐색 fallback가 있더라도 아래 핵심 참조는 Inspector에서 직접 연결하는 편이 안전합니다.

## `ModeManager`

- `defaultModeButton`
- `roomCreateModeButton`
- `detailEditModeButton`
- 필요 시 `doorInsertModeButton`
- 필요 시 `windowInsertModeButton`

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

## wall edit 계열

- `DrawManager`
- `HandleManager`
- `WallSelectionManager`
- `SnapManager`
- `WallPropertyInputManager`
- `WallOpeningPlacementManager`

다음 참조는 특히 비어 있으면 최근 수정 동작이 깨질 수 있습니다.

- `HandleManager.snapManager`
- `HandleManager.roomManager`
- `WallPropertyInputManager.handleManager`
- `WallPropertyInputManager.wallSelectionManager`
- `TopViewRenderManager.handleManager`
