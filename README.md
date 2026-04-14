# LH Editor Refactoring

Unity 기반 평면 편집기 리팩터링 프로젝트입니다.

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
  - room 클릭으로 선택
  - 선택된 room 드래그로 이동
  - room handle 드래그로 크기/형태 조절
  - room 속성 패널 표시
- `DetailEdit`
  - 벽 선택
  - 벽 길이/높이/속성 편집
  - 문/창 배치
- `DoorInsert`
- `WindowInsert`

## 주요 컴포넌트

- `ModeManager`
  - 편집 모드 전환
- `DrawManager`
  - 기본 벽 생성
- `HandleManager`
  - 벽 handle 생성/편집
- `WallSelectionManager`
  - 벽 선택 및 이동
- `RoomManager`
  - room 생성/삭제/목록 관리
- `RoomCreateManager`
  - room 생성, 선택, 이동, 분할
- `RoomHandleManager`
  - 선택된 room의 꼭짓점 handle 편집
- `RoomAuthoringPanelManager`
  - room 속성 패널, 면적 표시, room 라벨 관리
- `TopViewRenderManager`
  - top view 배치 렌더링
- `WallOpeningPlacementManager`
  - 문/창 배치
- `UndoRedoManager`
  - 편집 이력 관리
- `LhSceneExporter`
  - JSON export

## SampleScene 기준 루트 오브젝트

- `Managers`
- `_Screen`
- `_TopPlanContent`
- `_Walls`
- `_Rooms`
- `Grid`

`Managers` 아래에는 보통 `ModeManager`, `WallEditManager`, `RoomManager`, `OpeningManager`, `TopViewRenderManager`, `UndoRedoManager`, `DisplayManager`가 배치됩니다.

## 빠른 시작

1. Unity Editor `6000.0.66f2`로 프로젝트를 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 로드합니다.
3. Play Mode에서 아래를 순서대로 확인합니다.
   - `Default`에서 벽 생성/이동
   - `RoomCreate`에서 room 생성/선택/이동/크기 조절
   - `DetailEdit`에서 벽 상세 편집 및 문/창 배치
4. 내보내기 전에는 `docs/operations.md` 체크리스트를 확인합니다.

## 문서 안내

- `docs/architecture.md`
  - 현재 구조와 매니저 책임
- `docs/scene-mapping.md`
  - `SampleScene` 기준 오브젝트 배치
- `docs/inspector-mapping.md`
  - 주요 Inspector 연결 포인트
- `docs/workflows.md`
  - 기능별 동작 흐름
- `docs/operations.md`
  - 점검/운영 체크리스트
- `docs/export.md`
  - JSON export 관련 메모
- `docs/next-steps.md`
  - 남아 있는 정리/개선 항목

## 보관 문서

아래 문서는 현재 메인 구현 흐름이 아니라, 이전 설계 기록 또는 보관용 문서입니다.

- `docs/virtual-boundary-design.md`
- `docs/space-cut-merge-spec.md`
