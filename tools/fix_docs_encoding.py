from pathlib import Path


FILES = {
    "README.md": """# LH Editor Refactoring

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
""",
    "docs/architecture.md": """# Architecture

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
""",
    "docs/direct-connection-checklist.md": """# Direct Connection Checklist

Inspector에서 직접 연결해 두는 편이 좋은 참조 목록입니다.

## `ModeManager`

- `defaultModeButton`
- `roomCreateModeButton`
- `detailEditModeButton`
- 필요 시 `doorInsertModeButton`
- 필요 시 `windowInsertModeButton`

## `RoomManager` GameObject

### `RoomAuthoringPanelManager`

- `modeManager`
- `roomManager`
- `roomHandleManager`
- `topViewRenderManager`
- `roomTypeDropdown`
- `roomEditMenu`
- `roomAreaInputField`

### `RoomHandleManager`

- `mainCamera`
- `grid`
- `targetCanvas`
- `snapManager`
- `wallHandleManager`
- `roomManager`
- `modeManager`

### `RoomCreateManager`

- `mainCamera`
- `grid`
- `wallRoot`
- `roomManager`
- `snapManager`
- `wallHandleManager`
- `roomHandleManager`
- `modeManager`
- `undoRedoManager`

## `TopViewRenderManager`

- `roomManager`
- `roomAuthoringPanelManager`
- `wallOpeningPlacementManager`
- `modeManager`

자동 탐색 fallback이 있더라도, 위 참조는 직접 연결해 두는 것이 안전합니다.
""",
    "docs/export.md": """# Export Guide

## 목적

LH 씬 데이터를 JSON으로 내보내는 기능의 현재 기준을 정리한 문서입니다.

## 진입점

- `Assets/Scripts/Export/LhSceneExporter.cs`

## 전제

- 벽 데이터는 `Wall`과 관련 매니저에서 유지합니다.
- room 데이터는 `RoomManager`가 보관하는 `Room` 목록을 기준으로 수집합니다.
- room polygon은 현재 `RoomCreate` 흐름으로 작성된 데이터를 사용합니다.

## 점검 항목

- export 전 `RoomManager`에 최신 room이 반영돼 있는지 확인
- top view 표시와 실제 room polygon이 일치하는지 확인
- Undo/Redo 직후라면 상태를 한 번 더 확인

## 메모

- 예전 virtual boundary 기반 room 분할 실험은 현재 메인 export 경로가 아닙니다.
- 관련 설계 문서는 참고용 보관 문서로만 유지합니다.
""",
    "docs/inspector-mapping.md": """# Inspector Mapping

`SampleScene` 기준으로 자주 확인하는 Inspector 연결 요약입니다.

## `ModeManager`

- `Default Mode Button` -> `_Button_Default`
- `Room Create Mode Button` -> `_Button_Room`
- `Detail Edit Mode Button` -> `_Button_EditDetail`
- `Door Insert Mode Button` -> 필요 시 연결
- `Window Insert Mode Button` -> 필요 시 연결
- `Initial Mode` -> 보통 `Default`

## `RoomManager` GameObject

### `RoomManager`

- room prefab/material 관련 필드가 있다면 기존 프로젝트 설정 유지

### `RoomAuthoringPanelManager`

- `modeManager`
- `roomManager`
- `roomHandleManager`
- `topViewRenderManager`
- `roomTypeDropdown`
- `roomEditMenu`
- `roomAreaInputField`
- `roomTypeLabelPrefab`

### `RoomHandleManager`

- `mainCamera`
- `grid`
- `targetCanvas`
- `snapManager`
- `wallHandleManager`
- `roomManager`
- `modeManager`
- `showHandlesOnlyForFocusedRoom` -> `true` 권장

### `RoomCreateManager`

- `mainCamera`
- `grid`
- `wallRoot`
- `roomManager`
- `snapManager`
- `wallHandleManager`
- `roomHandleManager`
- `modeManager`
- `undoRedoManager`

## `TopViewRenderManager`

- `roomManager`
- `roomAuthoringPanelManager`
- `wallOpeningPlacementManager`
- `modeManager`
- top view 색상 필드
  - 기본 floor 색
  - 선택 room 색
  - wall 색
  - virtual boundary 색

## `WallEditManager` 계열

- `DrawManager`, `HandleManager`, `WallSelectionManager`, `SnapManager` 사이 참조가 비어 있지 않은지 확인
- `HandleManager`의 handle canvas가 wall 선보다 위에 그려지는지 확인

## 권장 확인 순서

1. `RoomAuthoringPanelManager.roomHandleManager`
2. `RoomCreateManager.roomHandleManager`
3. `RoomCreateManager.undoRedoManager`
4. `TopViewRenderManager.roomAuthoringPanelManager`
5. `ModeManager`의 room 버튼 연결
""",
    "docs/next-steps.md": """# Next Steps

현재 구조 기준으로 남아 있는 다음 작업 후보입니다.

## 1. `SampleScene` 재저장

이번 정리 과정에서 씬을 Unity 백업으로 복구했습니다.

- Unity Editor에서 `SampleScene.unity`를 열고
- 참조가 정상인지 확인한 뒤
- 한 번 저장해 두는 것이 좋습니다.

## 2. Scene UI 이름 정리

스크립트 역할은 `RoomAuthoringPanelManager`로 정리됐지만, 씬 안 패널 이름은 일부 예전 명칭이 남아 있을 수 있습니다.

- room 속성 패널 오브젝트 이름 정리
- 비활성 legacy 버튼이 남아 있는지 재확인

## 3. 남은 UI 오브젝트 최적화

- opening marker/label 풀링
- 화면 밖 label 숨김 정책 강화
- handle 재사용 구조 보강

## 4. room 작성 UX 개선

- room 생성 시 스냅 피드백 강화
- room 이동/크기 조절 시 제약 옵션 추가
- 직사각형 외 polygon room 작성 확장 검토

## 5. export 검증

- room polygon
- wall/opening 데이터
- JSON 구조

이 세 가지가 현재 작성 흐름과 맞는지 다시 점검할 필요가 있습니다.
""",
    "docs/operations.md": """# Operations

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
""",
    "docs/scene-mapping.md": """# Scene Mapping

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
""",
    "docs/scenes-and-references.md": """# Scenes And References

## 현재 사용 씬

- `Assets/Scenes/SampleScene.unity`

## room 작성에 직접 관여하는 참조

- `ModeManager`
- `RoomManager`
- `RoomCreateManager`
- `RoomHandleManager`
- `RoomAuthoringPanelManager`
- `TopViewRenderManager`
- `UndoRedoManager`

## 벽 편집에 직접 관여하는 참조

- `DrawManager`
- `HandleManager`
- `WallSelectionManager`
- `SnapManager`

## 메모

- scene는 백업 복구본 기준이므로, Unity에서 한 번 저장 후 참조 상태를 다시 확인하는 것이 좋습니다.
""",
    "docs/space-cut-merge-spec.md": """# Space Cut / Merge Spec

이 문서는 보관용 설계 메모입니다.

현재 메인 구현은 `RoomCreate` 기반 직접 room 작성 흐름이며, 이 문서의 virtual boundary 기반 분할/병합 설계는 활성 개발 경로가 아닙니다.

## 요약

- 과거에는 wall + virtual boundary 그래프로 room을 분할하려는 시도가 있었다.
- 현재는 복잡도를 줄이기 위해 room을 직접 생성/선택/이동/수정하는 방향으로 정리했다.
- 필요 시 이후에 room merge 기능만 별도 재설계하는 편이 더 적절하다.
""",
    "docs/virtual-boundary-design.md": """# Virtual Boundary Design

이 문서는 보관용 설계 기록입니다.

현재 메인 room 작성 흐름은 `RoomCreate` 기반이며, virtual boundary는 주력 authoring 수단이 아닙니다.

## 기록 목적

- 이전 room 분할 실험의 방향을 남겨 두기 위함
- 필요 시 추후 공간 분할 기능을 재검토할 때 참고하기 위함

## 현재 상태

- room 생성/선택/이동/크기 조절은 `RoomCreate` 중심으로 동작한다.
- virtual boundary 문서는 참고 자료로만 유지한다.
""",
    "docs/workflows.md": """# Workflows

현재 프로젝트의 대표 동작 흐름입니다.

## 1. 벽 작성

1. `Default` 모드 진입
2. 벽을 그리거나 endpoint handle로 조정
3. 필요하면 벽 자체를 선택해서 이동

## 2. room 작성

1. `RoomCreate` 모드 진입
2. 빈 공간을 드래그해서 room 생성
3. 기존 room을 클릭해 선택
4. room 내부 드래그로 이동
5. room handle 드래그로 크기/형태 조절
6. 우측 room 속성 패널에서 타입/면적 확인

## 3. room 선택 시 UI 동기화

1. `RoomHandleManager`가 focus room을 갱신
2. `RoomAuthoringPanelManager`가 선택 room을 동기화
3. room 타입 드롭다운과 면적 필드가 갱신
4. top view와 3D room이 강조색으로 표시

## 4. 벽 상세 편집

1. `DetailEdit` 모드 진입
2. 벽 선택
3. 길이/높이/속성 수정
4. 문/창 배치

## 5. top view 갱신

1. 벽/room/opening 데이터가 변경됨
2. 관련 매니저가 dirty 상태를 올림
3. `TopViewRenderManager`가 배치 그래픽을 다시 그림

## 6. export

1. 편집 상태 최종 확인
2. room/wall/opening 데이터 최신 상태 확인
3. exporter 실행
""",
}

for path, content in FILES.items():
    Path(path).write_text(content, encoding="utf-8", newline="\n")

print(f"rewrote {len(FILES)} files")
