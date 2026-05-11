# Code Reduction Roadmap

이 프로젝트의 현재 목표는 `DWG/PDF/이미지`를 바탕으로 `실측과 완전히 일치하지 않아도 되는 3D 공간`을 빠르게 만드는 것이다.

따라서 아래 원칙으로 코드를 줄이는 것이 맞다.

- 정밀 위상 복원보다 수동 보정 가능한 근사 배치를 우선한다.
- 런타임 편집기 구조 분해보다 기능 단순화와 유지보수 비용 절감을 우선한다.
- 파일 수가 많은 얇은 래퍼는 합친다.
- 외부 보관용 코드와 샘플 코드는 제품 코드 탐색 경로에서 뺀다.

## 1. 즉시 삭제 후보

### 1-1. 사용되지 않는 Room cycle 탐색 API

파일:

- `Assets/Scripts/Room/RoomGraphUtility.Cycle.cs`

우선 삭제 검토 대상 메서드:

- `BuildLargestCycleVerticesFromBoundaryGraph`
- `TryFindContainingCycle`
- `BuildBoundaryCycles`
- `BuildBoundaryFaces`
- `FindBoundaryCyclesDepthFirst`

근거:

- 프로젝트 검색 기준으로 위 메서드군은 `RoomGraphUtility.Cycle.cs` 내부에서만 사용된다.
- 실제 런타임에서 `RoomManager`는 `BuildPlanarGraph(...)`만 사용한다.
- cycle DFS는 정확한 폐곡선 탐색기 성격이 강하고, 현재 제품 목표에 비해 비용이 크다.

정리 방식:

- 1차: public이지만 외부 사용처 없는 cycle/face API 제거.
- 2차: `BuildPlanarGraphFromEdges`에 필요한 최소 보조 함수만 남기고 파일 축소.

예상 효과:

- 방 위상 유틸 복잡도 큰 폭 감소
- 디버깅 범위 축소
- 자동 방 생성 관련 오작동 원인 감소

### 1-2. 저장소 내 미사용/외부 보관 코드

정리 후보 경로:

- `Unused/`
- `tools/_external/`
- `Assets/ThirdParty/Paroxe/PDFRenderer/Examples/`

근거:

- `Assets/Scripts`: 약 33k lines
- `Unused`: 약 695 lines
- `tools/_external`: 약 141k lines
- `PDFRenderer/Examples`: 약 1k lines

정리 방식:

- 제품과 함께 빌드/탐색할 필요가 없다면 리포지토리 밖 아카이브로 이동
- 최소한 솔루션 탐색 기준에서는 제외
- `Unused`는 이름 그대로라면 실제 참조 확인 후 삭제

예상 효과:

- 코드 검색 잡음 대폭 감소
- 신규 작업자가 제품 경로를 더 빨리 파악 가능

## 2. 가장 먼저 합칠 후보

### 2-1. DWG import popup 분해 구조

현재 주 파일:

- `Assets/Scripts/Import/DwgWallImporter.cs`

현재 보조 파일:

- `Assets/Scripts/Import/DwgWallImportPopupBinder.cs`
- `Assets/Scripts/Import/DwgWallImportPopupPresenter.cs`
- `Assets/Scripts/Import/DwgWallImportPopupValidationService.cs`
- `Assets/Scripts/Import/DwgWallImportPopupCoordinator.cs`
- `Assets/Scripts/Import/DwgWallImportPopupController.cs`
- `Assets/Scripts/Import/DwgWallImportPopupState.cs`
- `Assets/Scripts/Import/DwgWallImportFacade.cs`

유지 검토 가능:

- `Assets/Scripts/Import/DwgWallImportExecutionBuilder.cs`
- `Assets/Scripts/Import/DwgWallImportProcessingService.cs`
- `Assets/Scripts/Import/DwgWallImportApplyService.cs`

근거:

- `DwgWallImporter`가 popup 관련 객체를 직접 모두 소유한다.
- Binder, Presenter, Validation, Coordinator는 대부분 단일 흐름을 포장하는 얇은 래퍼다.
- import의 본질은 `파일 선택 -> 레이어 선택 -> 세그먼트 추출 -> 벽 생성`이며, 현재 구조는 그 목적에 비해 분해가 과하다.

권장 구조:

- `DwgWallImporter`
- `DwgWallImportPopupView`
- `DwgWallImportProcessingService`
- `DwgWallImportApplyService`

정리 방식:

- popup state/binder/presenter/validation/coordinator를 `DwgWallImporter` 내부 private 메서드로 회수
- facade는 importer에 병합
- execution builder는 남겨도 되지만, 길이가 짧으면 importer 또는 apply service로 병합

예상 효과:

- import 흐름 추적 쉬움
- 파일 수 감소
- popup 관련 버그 수정 동선 단축

### 2-2. Wall selection 얇은 서비스 래퍼

주 파일:

- `Assets/Scripts/Draw/Wall/Core/WallSelectionManager.cs`

통합 우선 후보:

- `Assets/Scripts/Draw/Wall/Core/WallSelectionViewSyncService.cs`
- `Assets/Scripts/Draw/Wall/Core/WallSelectionPresentationController.cs`
- `Assets/Scripts/Draw/Wall/Core/WallSelectionEnvironmentService.cs`

2차 통합 후보:

- `Assets/Scripts/Draw/Wall/Core/WallSelectionQueryService.cs`
- `Assets/Scripts/Draw/Wall/Core/WallSelectionUndoRecorder.cs`

보류:

- `Assets/Scripts/Draw/Wall/Core/WallSelectionDragController.cs`
- `Assets/Scripts/Draw/Wall/Core/WallSelectionInputController.cs`
- `Assets/Scripts/Draw/Wall/Core/WallSelectionMutationService.cs`

근거:

- 일부 파일은 사실상 전달 전용이다.
- `WallSelectionViewSyncService`는 presentation controller 호출을 감싸는 수준이다.
- `WallSelectionEnvironmentService`는 reference resolve와 drag plane 계산 정도만 담당한다.

정리 방식:

- 전달 전용 wrapper부터 제거
- `WallSelectionManager` 내부 private 메서드로 회수
- query/undo는 길이와 응집도를 보고 남길지 결정

예상 효과:

- 선택 로직 추적 경로 단축
- 불필요한 객체 수 감소

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
- `OverlayFileDialog`
- `PdfThumbnailLoader`

추가 단순화:

- 가능하면 패널은 코드 생성 대신 prefab 기반으로 전환
- PDF thumbnail은 Paroxe 한 경로만 유지하고 Shell fallback 제거 검토

## 5. 실제 실행 순서

### Phase 1

- `Unused/` 실제 참조 확인 후 제거
- `tools/_external/`와 `PDFRenderer/Examples/`를 제품 경로에서 분리
- `RoomGraphUtility.Cycle.cs` unused API 제거

### Phase 2

- `DwgWallImporter` popup 보조 클래스 병합
- `WallSelectionViewSyncService`, `WallSelectionPresentationController`, `WallSelectionEnvironmentService` 회수

### Phase 3

- `RoomCreateManager`의 `TrySplitContainingRoom` 제거 여부 결정
- `CollectWallsMatchingPolygon`를 optional 경로로 축소
- `RoomManager.RefreshAllRooms()` 자동 재구성 범위 축소

### Phase 4

- `DrawingOverlaySceneBootstrap.cs` 분해
- PDF fallback 단일화
- 런타임 파일 다이얼로그 공통화

## 6. 바로 작업 시작하기 좋은 항목

위험도가 낮고 효과가 큰 순서:

1. `RoomGraphUtility.Cycle.cs`의 미사용 API 삭제
2. `DwgWallImporter` popup helper 병합
3. `WallSelectionViewSyncService` 제거
4. `Unused/` 정리
5. `tools/_external/` 분리

## 7. 보류하는 것이 나은 항목

- `RoomPlanarGraph` 자체 삭제
- `RoomManager` 전체 재설계
- `SnapManager`의 spatial hash 제거

이 항목들은 실제 편집 품질 저하 가능성이 있어, 먼저 상위 레이어를 정리한 뒤 판단하는 편이 안전하다.
