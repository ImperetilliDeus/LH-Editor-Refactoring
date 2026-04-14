# Operations

실행 전후로 빠르게 확인하는 운영 체크리스트입니다.

## Play Mode 전

- `SampleScene.unity`가 열려 있는지 확인
- `ModeManager` 버튼 연결 확인
- `RoomManager` / `RoomCreateManager` / `RoomHandleManager` / `RoomAuthoringPanelManager` 참조 확인
- `TopViewRenderManager`가 `_TopPlanContent`를 가리키는지 확인

## 기본 기능 점검

### `Default`

- 벽 생성 가능
- 벽 handle 드래그 시 3D와 top view가 함께 즉시 갱신
- handle이 wall 선분 위에 잘 보임

### `RoomCreate`

- 빈 공간 드래그로 room 생성 가능
- 생성된 room 클릭 시 선택됨
- 선택된 room handle이 나타남
- room 내부 드래그로 전체 이동 가능
- handle 드래그로 크기 조절 가능
- 우클릭으로 선택 해제 가능

### `DetailEdit`

- 벽 선택 가능
- 벽 길이/높이/속성 패널이 갱신됨
- 문/창 배치가 정상 동작

## top view 점검

- room fill이 정상 표시됨
- 선택 room은 강조색으로 보임
- wall과 virtual boundary가 누락 없이 보임
- label이 과도하게 쌓이지 않는지 확인

## 저장/내보내기 전

- Undo/Redo 후 상태가 정상인지 확인
- room 수와 실제 화면이 일치하는지 확인
- export 대상 데이터가 최신 상태인지 확인
