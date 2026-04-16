# LH Editor Refactoring

Unity 기반 평면/벽/방 편집기 리팩터링 프로젝트입니다.

현재 편집 흐름은 `벽 작성 -> RoomCreate로 공간 작성/이동/크기 조절 -> DetailEdit로 벽 상세 수정 -> Export` 순서로 정리되어 있습니다. 예전의 `RoomSelect`, `SpaceCut`, `EditRoom` 중심 흐름은 제거되었고, 공간 작성은 `RoomCreate` 하나로 통합되었습니다.

## 환경

- Unity Editor: `6000.0.66f2`
- 메인 씬: `Assets/Scenes/SampleScene.unity`
- 스크립트 루트: `Assets/Scripts`

## 현재 모드

- `Default`
  - 벽 생성
  - 벽 endpoint handle 편집
- `RoomCreate`
  - 빈 공간 드래그로 room 생성
  - room 클릭 선택
  - 선택한 room 드래그 이동
  - room handle 드래그로 크기/형태 조절
- `DetailEdit`
  - 벽 선택
  - 벽 길이/높이/두께 편집
  - 문/창 배치
  - 다중 선택 박스
- `DoorInsert`
- `WindowInsert`

## 주요 컴포넌트

- `ModeManager`
  - 편집 모드 전환
- `DrawManager`
  - 기본 벽 생성과 preview wall 표시
- `HandleManager`
  - 벽 endpoint handle 생성/드래그
  - split point drag 처리
  - 연결 벽 기준 3D end-cap 재계산
- `SnapManager`
  - 그리드/축/handle/벽 segment snap 관리
  - handle snap은 기본적으로 `Ctrl` 입력 시에만 동작
- `WallSelectionManager`
  - DetailEdit 벽 선택과 이동
- `WallPropertyInputManager`
  - 선택된 벽 길이/높이/두께 입력 적용
  - 변경 즉시 top view / room / handle 갱신
- `RoomManager`
  - room 생성/삭제/목록 관리
- `RoomCreateManager`
  - room 생성, 선택, 이동, 크기 조절
- `RoomHandleManager`
  - 선택된 room polygon handle 편집
- `TopViewRenderManager`
  - `_TopPlanContent` UI 렌더링
- `WallOpeningPlacementManager`
  - 문/창 배치와 opening container 재구성
- `UndoRedoManager`
  - 편집 이력 관리
- `LhSceneExporter`
  - JSON export

## 최근 반영된 동작

- DetailEdit에서 벽 속성 변경 시 화면과 top view가 즉시 갱신됩니다.
- DetailEdit에서 생성되던 불필요한 파란 handle 재등장을 막았습니다.
- handle snap은 상시가 아니라 `Ctrl` 키 입력 중에만 적용됩니다.
- T자 접합의 split point handle은 붙어 있는 면을 따라 슬라이드하도록 보강되었습니다.
- top view 벽 표시에는 두께 기준 cap extension이 적용됩니다.
- 3D wall object는 연결된 벽의 방향과 두께를 참고해 endpoint별 end-cap 길이를 다시 계산합니다.
- room의 `Floor` 시각 오브젝트는 월드 기준 `y = 0.1`에 배치됩니다.

## SampleScene 기준 루트 오브젝트

- `Managers`
- `_Screen`
- `_TopPlanContent`
- `_Walls`
- `_Rooms`
- `Grid`

## 빠른 시작

1. Unity Editor `6000.0.66f2`로 프로젝트를 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 로드합니다.
3. Play Mode에서 아래 흐름을 확인합니다.
   - `Default`에서 벽 생성/endpoint 편집
   - `RoomCreate`에서 room 생성/선택/이동/크기 조절
   - `DetailEdit`에서 벽 상세 편집과 문/창 배치
4. 세부 점검은 `docs/operations.md`를 확인합니다.

## 문서 안내

- `docs/architecture.md`
  - 현재 구조와 매니저 책임
- `docs/workflows.md`
  - 기능별 동작 흐름
- `docs/operations.md`
  - 수동 점검 체크리스트
- `docs/inspector-mapping.md`
  - SampleScene 기준 Inspector 연결 포인트
- `docs/scene-mapping.md`
  - SampleScene 오브젝트 배치
- `docs/conventions.md`
  - 코드/문서 작성 규칙
- `docs/export.md`
  - export 경로와 확인 포인트
- `docs/next-steps.md`
  - 남은 개선 과제

## 보조 문서

아래 문서는 현재 메인 구현 설명이라기보다 설계 메모/참조 성격이 강합니다.

- `docs/virtual-boundary-design.md`
- `docs/space-cut-merge-spec.md`
- `docs/scenes-and-references.md`
- `docs/direct-connection-checklist.md`
