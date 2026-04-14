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

현재 room 작성 흐름은 `RoomManager` 오브젝트 아래 컴포넌트 조합으로 이뤄집니다.

- `RoomManager`
- `RoomAuthoringPanelManager`
- `RoomHandleManager`
- `RoomCreateManager`

## 참고

- scene는 이번 정리 중 Unity 백업에서 복구한 버전입니다.
- 따라서 Inspector 참조는 살아 있어도, 오브젝트 이름 일부는 예전 명칭이 남아 있을 수 있습니다.
