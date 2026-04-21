# LH Editor Refactoring

Unity 기반 평면 / 벽 / room 편집기 리팩터링 프로젝트입니다.

현재 편집 흐름은 `Default -> RoomCreate -> DetailEdit -> Export` 순서로 정리되어 있습니다.

## Environment

- Unity Editor: `6000.0.66f2`
- Main Scene: `Assets/Scenes/SampleScene.unity`
- Script Root: `Assets/Scripts`

## Modes

- `Default`
  - 벽 생성
  - 벽 endpoint handle 편집
- `RoomCreate`
  - 빈 공간 드래그로 room 생성
  - room 선택 / 이동 / 크기 조정
- `DetailEdit`
  - 벽 선택
  - 벽 길이 / 높이 / 두께 편집
  - 문 / 창 배치
  - 다중 선택 박스
- `DoorInsert`
- `WindowInsert`
- `FurniturePlace`

## Key Components

- `ModeManager`
  - 현재 편집 모드 상태 관리
  - `ModeChanged` 이벤트 발행
- `ModeButtonBinder`
  - UI `Button`과 `EditorMode`를 연결
  - 각 버튼이 스스로 `ModeManager`에 등록
- `DrawManager`
  - 기본 벽 생성과 preview wall 표시
  - wall tool 전환은 `WallToolController`를 통해 처리
- `HandleManager`
  - 벽 endpoint handle 생성 / 드래그
  - split point drag 처리
- `SnapManager`
  - grid / axis / handle / wall segment snap 관리
- `WallSelectionManager`
  - `DetailEdit` 벽 선택과 이동
- `WallPropertyInputManager`
  - 선택된 벽 길이 / 높이 / 두께 입력 반영
- `RoomManager`
  - room 생성 / 삭제 / 목록 관리
- `RoomCreateManager`
  - room 생성, 선택, 이동, 크기 조정
- `RoomHandleManager`
  - 선택된 room polygon handle 편집
- `TopViewRenderManager`
  - `_TopPlanContent` UI 렌더링
- `WallOpeningPlacementManager`
  - 문 / 창 배치와 opening container 관리
- `UndoRedoManager`
  - 편집 이력 관리
- `LhSceneExporter`
  - JSON export

## Input Layer

- 공통 입력 추상화: `IEditorInputProvider`
- 기본 구현: `UnityEditorInputProvider`
- wall 계열 포인터 프레임 조립: `PointerInputFrameUtility`

이 구조로 `DrawManager`, `HandleManager`, `WallSelectionManager`, `SnapManager`가 더 이상 직접 static 입력 API에 강하게 묶이지 않도록 정리되어 있습니다.

## SampleScene Notes

- `ModeManager`는 버튼 필드를 직접 들고 있지 않습니다.
- `_Button_Default`, `_Button_Room`, `_Button_EditDetail`, `_Button_EditFurnish`는 각각 `ModeButtonBinder`로 모드를 등록합니다.
- 주요 root 오브젝트:
  - `Managers`
  - `_Screen`
  - `_TopPlanContent`
  - `_Walls`
  - `_Rooms`
  - `Grid`

## Quick Start

1. Unity Editor `6000.0.66f2`로 프로젝트를 엽니다.
2. `Assets/Scenes/SampleScene.unity`를 로드합니다.
3. Play Mode에서 아래 흐름을 확인합니다.
   - `Default`에서 벽 생성 / endpoint 편집
   - `RoomCreate`에서 room 생성 / 선택 / 이동 / 크기 조정
   - `DetailEdit`에서 벽 상세 편집과 문 / 창 배치
4. 세부 체크는 `docs/operations.md`를 확인합니다.

## Docs

- `docs/architecture.md`
  - 현재 구조와 매니저 책임
- `docs/workflows.md`
  - 기능별 동작 흐름
- `docs/operations.md`
  - 수동 점검 체크리스트
- `docs/inspector-mapping.md`
  - SampleScene 기준 Inspector 연결
- `docs/scene-mapping.md`
  - SampleScene 오브젝트 배치
- `docs/conventions.md`
  - 코드 / 문서 작성 규칙
- `docs/export.md`
  - export 경로와 확인 사항
- `docs/next-steps.md`
  - 후속 개선 과제

## Reference Docs

아래 문서는 현재 메인 구현 설명보다 설계 메모 / 참고 성격이 더 강합니다.

- `docs/virtual-boundary-design.md`
- `docs/space-cut-merge-spec.md`
- `docs/scenes-and-references.md`
- `docs/direct-connection-checklist.md`
