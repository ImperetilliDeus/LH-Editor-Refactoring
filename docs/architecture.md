# Architecture

## 목적

현재 프로젝트의 실제 구조와 책임 분리를 빠르게 파악하기 위한 문서입니다.

이 문서는 예전 `RoomSelect` / `SpaceCut` 중심 흐름이 아니라, 현재 유지 중인 `RoomCreate` 중심 흐름을 기준으로 설명합니다.

## 현재 편집 모드

- `Default`
- `RoomCreate`
- `DetailEdit`
- `DoorInsert`
- `WindowInsert`

`ModeManager`는 예전 enum 값이 직렬화돼 있어도 런타임에서 `RoomCreate`로 정규화합니다.

## 핵심 책임 분리

### `ModeManager`

- 현재 모드 관리
- 모드 버튼 연결
- 모드 변경 이벤트 발행

### `DrawManager`

- `Default` 모드에서 벽 생성
- 벽 미리보기와 길이 표시

### `HandleManager`

- 벽 endpoint handle 표시
- 벽 형태 수정
- 드래그 중 top view 갱신

### `WallSelectionManager`

- 벽 선택
- 벽 전체 이동
- 선택 상태 유지

### `RoomManager`

- room 생성/삭제/목록 관리
- polygon 기반 room geometry 갱신
- `RoomsChanged` 이벤트 발행

### `RoomCreateManager`

현재 room 작성의 중심 입력 매니저입니다.

- 드래그로 room 생성
- room 클릭 선택
- 선택된 room 드래그 이동
- 사각 room 분할 생성
- 우클릭 선택 해제

### `RoomHandleManager`

- 선택된 room의 꼭짓점 handle만 표시
- handle 드래그로 room polygon 수정
- focus room 관리

### `RoomAuthoringPanelManager`

이전 `RoomEditManager`의 후속 역할입니다.

- 선택된 room과 속성 패널 동기화
- room 타입 드롭다운 반영
- room 면적 표시
- room 라벨 표시/재사용
- 선택 room 하이라이트 색 반영

### `TopViewRenderManager`

- top view 전용 2D 렌더링
- wall / room floor / virtual boundary를 배치 그래픽으로 렌더링
- 선택 room 색상 강조

### `WallOpeningPlacementManager`

- 문/창 삽입
- opening marker와 상세 UI 관리

### `UndoRedoManager`

- 벽, room, opening 편집 이력 관리

### `LhSceneExporter`

- 씬 데이터를 JSON으로 내보내는 export 경로

## 현재 room 작성 흐름

1. 사용자가 `RoomCreate` 모드로 진입합니다.
2. 빈 공간을 드래그하면 직사각형 room이 생성됩니다.
3. 기존 room을 클릭하면 선택되고 handle이 나타납니다.
4. 선택된 room을 드래그하면 room 전체가 이동합니다.
5. handle을 드래그하면 room polygon이 수정됩니다.
6. `RoomAuthoringPanelManager`가 타입/면적/라벨을 갱신합니다.

## top view 구조

현재 `_TopPlanContent`에는 room/wall마다 개별 UI 오브젝트를 만들지 않고, 주요 레이어를 배치 그래픽으로 합쳐 그립니다.

- wall: 배치 세그먼트 그래픽
- virtual boundary: 배치 세그먼트 그래픽
- room floor: 배치 polygon 그래픽

이 구조는 Hierarchy 증가와 Canvas rebuild 비용을 줄이기 위한 것입니다.

## 남아 있는 기술 부채

- `SampleScene.unity`는 현재 백업 복구본 기준이므로, Unity에서 다시 저장해 텍스트 직렬화 상태를 확인할 필요가 있습니다.
- `RoomAuthoringPanelManager`의 역할은 정리됐지만, UI 오브젝트 이름과 씬 내 패널 이름은 일부 예전 명칭이 남아 있을 수 있습니다.
