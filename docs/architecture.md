# Architecture

## 목적

현재 프로젝트의 주요 구조와 각 매니저의 책임을 빠르게 파악하기 위한 문서입니다.

## 편집 모드

- `Default`
- `RoomCreate`
- `DetailEdit`
- `DoorInsert`
- `WindowInsert`
- `FurniturePlace`

## 책임 분리

### `ModeManager`

- 현재 편집 모드 관리
- `ModeChanged` 이벤트 발행

### `ModeButtonBinder`

- UI `Button`과 `EditorMode`를 1:1로 바인딩
- 개별 버튼이 스스로 `ModeManager`에 등록
- `ModeManager`가 구체 UI 버튼 목록을 직접 소유하지 않음

### `DrawManager`

- `Default` 모드에서 벽 생성
- preview wall 표시
- 생성된 벽을 `HandleManager`에 등록
- wall tool 전환은 `WallToolController`를 통해 처리

### `HandleManager`

- 벽 endpoint handle 표시와 드래그
- vertex group 구성과 merge/split 관리
- split point drag와 chain drag 처리
- wall 변경 후 handle 구조와 3D end-cap 관계 갱신

### `SnapManager`

- grid snap / axis snap / handle snap / wall segment snap 제공
- handle drag grid snap과 일반 handle snap modifier를 분리 관리
- modifier 입력은 `IEditorInputProvider` 경로로 조회

### `WallSelectionManager`

- `DetailEdit`에서 벽 선택과 다중 선택
- 벽 전체 이동
- 연결된 wall opening container drag 적용
- 입력 프레임 생성은 provider 기반으로 통일

### `WallPropertyInputManager`

- 길이 / 높이 / 두께 입력 적용
- 선택된 벽 또는 opening container 갱신
- 변경 직후 handle / room / top view 즉시 갱신

### `TopViewRenderManager`

- `_TopPlanContent`에 2D UI 렌더링
- room floor polygon, wall segment, virtual boundary, opening marker 표시

### `RoomManager`

- room 생성 / 삭제 / 목록 관리
- `RoomsChanged` 이벤트 발행
- room polygon과 시각 오브젝트 갱신

### `WallOpeningPlacementManager`

- 문 / 창 배치
- opening container와 wall segment 관계 관리
- opening marker / 선택 UI 관리

### `UndoRedoManager`

- wall / room / opening 변경 이력 관리
- snapshot 복원 시 wall / handle 구조 동기화

## 입력 계층

- 공통 입력 추상화는 `IEditorInputProvider`
- 기본 구현은 `UnityEditorInputProvider`
- wall 계열 입력 프레임 조립은 `PointerInputFrameUtility`에서 공통 처리

## 현재 한계

- 3D wall join은 현재 end-cap 보정 기반입니다.
- 복잡한 각도와 특수 접합에서 완전한 miter mesh가 필요하면 wall 본체 mesh를 더 가변적으로 교체하는 후속 작업이 필요합니다.
