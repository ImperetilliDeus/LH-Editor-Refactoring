# Code Reduction Roadmap

이 프로젝트의 현재 목표는 `DWG/PDF/이미지`를 바탕으로 `실측과 완전히 일치하지 않아도 되는 3D 공간`을 빠르게 만드는 것이다.

따라서 아래 원칙으로 코드를 줄이는 것이 맞다.

- 정밀 위상 복원보다 수동 보정 가능한 근사 배치를 우선한다.
- 런타임 편집기 구조 분해보다 기능 단순화와 유지보수 비용 절감을 우선한다.
- 파일 수가 많은 얇은 래퍼는 합친다.
- 외부 보관용 코드와 샘플 코드는 제품 코드 탐색 경로에서 뺀다.

## 1. 즉시 삭제 후보

### 1-1. 사용되지 않는 Room cycle 탐색 API (완료)

`BuildLargestCycleVerticesFromBoundaryGraph`, `TryFindContainingCycle`, `BuildBoundaryCycles`, `BuildBoundaryFaces`, `FindBoundaryCyclesDepthFirst`는 이미 삭제되었다.

- `RoomGraphUtility.Cycle.cs`에 남은 메서드는 모두 `BuildPlanarGraph` 경로에서 쓰인다.
- 이 파일에 따로 있던 정점 정리 함수는 2026-11 `PolygonXZ`로 통합되었다.

### 1-2. 저장소 내 미사용/외부 보관 코드 (완료)

- `Unused/`와 `tools/_external/`는 저장소에서 정리되었다.
- `Assets/ThirdParty/Paroxe/PDFRenderer/Examples/`는 ThirdParty 수정 금지 규칙(CLAUDE.md)에 따라 남긴다. 정리가 필요하면 별도로 보고한다.

## 2. 가장 먼저 합칠 후보

### 2-1. DWG import popup 분해 구조 (완료)

popup 보조 클래스(Binder, Presenter, Validation, Coordinator, Controller, State, Facade)는 병합되었다. 현재 구조는 다음과 같다.

- `DwgWallImporter`
- `DwgWallImportPopupView`
- `DwgWallImportExecutionBuilder`
- `DwgWallImportProcessingService`
- `DwgWallImportApplyService`
- `DwgWallImportSceneApplier`

### 2-2. Wall selection 얇은 서비스 래퍼 (완료)

- `WallSelectionViewSyncService`, `WallSelectionEnvironmentService`, `WallSelectionQueryService`, `WallSelectionUndoRecorder`, `WallSelectionMutationService`는 삭제되었다.
- `WallSelectionDragController`, `WallSelectionInputController`와 각 state 클래스는 2026-11 벽 선택 단순화(단일 선택, 본체 드래그·박스 선택 제거)로 삭제되었다.
- `WallSelectionPresentationController`는 선택 UI proxy의 생성·삭제·위치 갱신만 남겼다. 단순 전달 메서드는 `WallSelectionManager`로 회수했다.

## 3. 기능 단순화 우선 후보

### 3-1. Room 자동 정밀 매칭 낮추기

핵심 파일:

- `Assets/Scripts/Room/RoomCreateManager.cs`
- `Assets/Scripts/Room/RoomManager.cs`

낮출 대상:

- `autoWallMatchMaxAngleDegrees`
- `autoWallMatchDistanceThreshold`
- `autoWallMatchMinOverlapRatio`
- `autoWallMatchMinOverlapLength`
- `CollectWallsMatchingPolygon`
- `TrySplitContainingRoom`
- room centroid 기반 자동 room 재매칭

근거:

- 현재 로직은 방 경계와 기존 벽의 정합성을 비교적 엄밀하게 추적한다.
- 사각형 분할, 벽 경계 overlap, centroid 기반 재매칭은 “도면 근사 재현”보다 “구조 정합 유지” 쪽 설계다.

권장 방향:

- 방은 수동 폴리곤 우선
- 벽 매칭은 optional
- 자동 room 재생성은 꺼두거나 editor 메뉴에서만 수동 실행

최소 변경안:

- `allowAutomaticRoomGeneration`를 사실상 비활성 정책으로 고정
- `TrySplitContainingRoom` 제거 또는 feature flag 뒤로 이동
- `CollectWallsMatchingPolygon` 실패 시 room 생성이 계속 되도록 단순화

강한 변경안:

- room 생성 시 `wallSet` 의존도를 낮추고 수동 room만 유지
- `RefreshAllRooms()`의 자동 face 매칭 경로 축소

예상 효과:

- 오버엔지니어링 제거
- room 생성 실패 케이스 감소
- 사용자가 약간 틀린 도면에도 작업 지속 가능

### 3-2. Snap 기능 줄이기

파일:

- `Assets/Scripts/Draw/Wall/Core/SnapManager.cs`

유지 추천:

- grid snap
- endpoint snap

축소 추천:

- wall segment snap
- pixel distance 기반 handle snap
- spatial hash 기반 과한 근접 탐색 튜닝

근거:

- 현재 제품에서는 빠른 벽 배치가 더 중요하다.
- 세그먼트 정합 스냅은 정교하지만 예기치 않은 흡착을 만들 수 있다.

권장 방향:

- 기본값은 단순 snap
- 고급 snap은 필요 시 옵션으로만 유지

## 4. 파일 분리 과한 구간

### 4-1. Overlay giant bootstrap

파일:

- `Assets/Scripts/Overlay/DrawingOverlaySceneBootstrap.cs`

현재 포함 책임:

- overlay system bootstrap
- calibration panel 생성
- import controller
- 파일 다이얼로그
- PowerShell 인코딩 스크립트 생성
- PDF thumbnail fallback
- Windows Shell interop

문제:

- 한 파일이 런타임 진입점, UI 생성기, OS interop, PDF fallback까지 가진다.
- 수정 범위가 너무 넓다.

권장 구조:

- `DrawingOverlaySceneBootstrap`
- `DrawingOverlayImportController`
- `OverlayCalibrationPanelFactory`
- `PdfThumbnailLoader`

파일 대화상자는 2026-11에 `Common/Platform/NativeFileDialog`로 공통화되었다. DWG import, 오버레이 import, 작업 상태 저장·불러오기, JSON export가 이것을 함께 쓴다.

추가 단순화:

- 가능하면 패널은 코드 생성 대신 prefab 기반으로 전환
- PDF thumbnail은 Paroxe 한 경로만 유지하고 Shell fallback 제거 검토

## 5. 실제 실행 순서

### Phase 1 (완료)

- `Unused/` 제거, `tools/_external/` 분리
- `RoomGraphUtility.Cycle.cs` unused API 제거

### Phase 2 (완료)

- `DwgWallImporter` popup 보조 클래스 병합
- Wall selection 서비스 래퍼 회수

### Phase 3

- `RoomCreateManager`의 `TrySplitContainingRoom` 제거 여부 결정
- `CollectWallsMatchingPolygon`를 optional 경로로 축소
- `RoomManager.RefreshAllRooms()` 자동 재구성 범위 축소

### Phase 4

- `DrawingOverlaySceneBootstrap.cs` 분해
- PDF fallback 단일화
- 런타임 파일 다이얼로그 공통화 (완료, `NativeFileDialog`)

## 6. 바로 작업 시작하기 좋은 항목

1~2절의 즉시 삭제·병합 후보는 모두 처리되었다. 남은 후보는 3절(기능 단순화)과 4절(Overlay bootstrap 분해)이며, 둘 다 동작이 바뀌므로 범위를 먼저 정한 뒤 진행한다.

## 7. 보류하는 것이 나은 항목

- `RoomPlanarGraph` 자체 삭제
- `RoomManager` 전체 재설계
- `SnapManager`의 spatial hash 제거

이 항목들은 실제 편집 품질 저하 가능성이 있어, 먼저 상위 레이어를 정리한 뒤 판단하는 편이 안전하다.
