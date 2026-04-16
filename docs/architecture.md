# Architecture

## 목적

현재 프로젝트의 실제 구조와 각 매니저의 책임을 빠르게 파악하기 위한 문서입니다.

## 편집 모드

- `Default`
- `RoomCreate`
- `DetailEdit`
- `DoorInsert`
- `WindowInsert`

## 핵심 책임 분리

### `ModeManager`

- 현재 편집 모드 관리
- 모드 버튼 연결
- `ModeChanged` 이벤트 발행

### `DrawManager`

- `Default` 모드에서 벽 생성
- preview wall 표시
- 새 벽을 `HandleManager`에 등록

### `HandleManager`

- 벽 endpoint handle 표시와 드래그
- vertex group 구성과 merge/split 관리
- split point drag와 chain drag 처리
- wall 변경 시 handle 재구성
- 연결된 벽을 기준으로 3D end-cap 재계산

### `SnapManager`

- grid snap / axis snap / handle snap / wall segment snap 제공
- handle drag grid snap과 일반 handle snap modifier를 분리 관리
- 현재 기본 정책은 `Ctrl` 입력 시 handle snap 활성화

### `WallSelectionManager`

- DetailEdit에서 벽 선택과 다중 선택
- 벽 전체 이동
- 연결된 벽/컨테이너 drag 적용

### `WallPropertyInputManager`

- 길이/높이/두께 입력 적용
- 선택된 벽 또는 opening container 재구성
- 변경 직후 handle / room / top view를 즉시 갱신

### `TopViewRenderManager`

- `_TopPlanContent`에 2D UI 렌더링
- room floor polygon, wall segment, virtual boundary, opening marker 표시
- wall 시각 표현에는 두께 기준 cap extension이 들어감

### `RoomManager`

- room 생성/삭제/목록 관리
- `RoomsChanged` 이벤트 발행
- room polygon 및 시각 오브젝트 갱신

### `Room`

- polygon 기반 floor mesh 생성
- room 루트 transform 관리
- `Floor` 자식 오브젝트 생성
- floor 시각 오브젝트는 월드 `y = 0.1`에 배치

### `WallOpeningPlacementManager`

- 문/창 배치
- opening container wall segment 재구성
- opening marker / 선택 UI 관리

### `UndoRedoManager`

- wall / room / opening 변경 이력 저장
- snapshot 복원 시 wall/handle 구조 재동기화

## 데이터와 렌더링

- 편집 로직은 `Wall.StartPoint`, `Wall.EndPoint`, vertex id 기준으로 동작합니다.
- 2D top view와 3D wall object는 같은 wall 데이터를 기반으로 하되, 시각 보정은 각 렌더러가 별도로 가질 수 있습니다.
- 최근 변경으로 3D wall object에는 endpoint별 end-cap 시각 보정이 들어갔고, 길이는 인접 벽 방향/두께를 기준으로 계산됩니다.

## 현재 알려진 한계

- 3D wall join은 현재 end-cap 보정 기반입니다.
- 복잡한 각도나 특수 접합에서 완전한 miter mesh가 필요하면 wall 본체 mesh 자체를 가변형으로 교체하는 후속 작업이 필요할 수 있습니다.
