# Scene Mapping

`Assets/Scenes/SampleScene.unity` 기준 오브젝트 배치 개요입니다.

## 루트

- `Managers`
- `_Screen`
- `_TopPlanContent`
- `_Walls`
- `_Rooms`
- `Grid`

## `Managers` 하위 주요 오브젝트

- `ModeManager`
- `WallEditManager`
- `RoomManager`
- `OpeningManager`
- `TopViewRenderManager`
- `UndoRedoManager`
- `DisplayManager`

## room 관련 구성

- `RoomManager`
- `RoomAuthoringPanelManager`
- `RoomHandleManager`
- `RoomCreateManager`

## wall 관련 구성

- `DrawManager`
- `HandleManager`
- `SnapManager`
- `WallSelectionManager`
- `WallPropertyInputManager`
- `WallOpeningPlacementManager`

## 메모

- `_TopPlanContent`는 실제 2D top view 표시 대상입니다.
- `_Walls` 아래 실제 3D wall object가 있고, 최근 join 보정은 이 wall object에 시각적으로 적용됩니다.
- `_Rooms` 아래 room root와 `Floor` 자식 오브젝트가 생성되며, `Floor`는 월드 `y = 0.1`에 배치됩니다.
