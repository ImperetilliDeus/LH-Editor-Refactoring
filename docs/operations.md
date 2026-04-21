# Operations

Play Mode에서 빠르게 확인하는 수동 점검 체크리스트입니다.

## 공통

- `SampleScene.unity`가 열려 있는지 확인
- `_Button_Default`, `_Button_Room`, `_Button_EditDetail`, `_Button_EditFurnish`에 `ModeButtonBinder`가 붙어 있는지 확인
- 각 `ModeButtonBinder.targetMode` 값이 UI 의도와 맞는지 확인
- `TopViewRenderManager`가 `_TopPlanContent`를 가리키는지 확인
- `HandleManager`, `WallSelectionManager`, `WallPropertyInputManager`, `SnapManager` 참조 확인

## `Default`

- 벽 생성 가능
- endpoint handle 드래그 가능
- handle 드래그 중 3D와 top view가 즉시 갱신
- handle snap은 `Ctrl` 입력 중에만 동작
- split point handle은 제한된 축을 따라 이동

## `RoomCreate`

- 빈 공간 드래그로 room 생성 가능
- 생성된 room 선택 가능
- room handle 드래그로 polygon 수정 가능
- floor 시각 오브젝트가 world `y = 0.1`에 배치되는지 확인

## `DetailEdit`

- 벽 선택 가능
- 속성 입력 변경이 추가 클릭 없이 즉시 반영
- DetailEdit에서는 기본 handle이 다시 나타나지 않음
- 두께가 다른 벽이 만나는 코너에서 top view와 3D가 모두 깨지지 않는지 확인
- 문/창 배치가 정상 동작

## Top View

- room fill이 정상 표시
- 선택 room / 선택 wall이 강조 색으로 보임
- wall join이 두께 기준으로 끊기지 않음
- label이 과도하게 겹치지 않음

## 마무리

- Undo/Redo 후 상태가 유지되는지 확인
- room / wall / opening 데이터가 화면과 일치하는지 확인
- export 시 최신 상태가 반영되는지 확인

참고:
- wall 계열 입력은 `IEditorInputProvider` 경로로 통일되어 있어, 입력 이상 시 provider 주입 상태를 먼저 확인합니다.
