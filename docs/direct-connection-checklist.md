# Direct Connection Checklist

Inspector에서 직접 연결해 두는 편이 좋은 참조 목록입니다.

## `ModeManager`

- `defaultModeButton`
- `roomCreateModeButton`
- `detailEditModeButton`
- 필요 시 `doorInsertModeButton`
- 필요 시 `windowInsertModeButton`

## `RoomManager` GameObject

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

## `TopViewRenderManager`

- `roomManager`
- `roomAuthoringPanelManager`
- `wallOpeningPlacementManager`
- `modeManager`

자동 탐색 fallback이 있더라도, 위 참조는 직접 연결해 두는 것이 안전합니다.
