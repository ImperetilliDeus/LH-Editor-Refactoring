# 기능별 분석 및 완성도

## 1. 요약

| 기능 | 완성도 | 판단 |
|---|---|---|
| 벽 작성/편집 | 높음 | 구현 범위와 테스트가 넓고 핵심 편집 흐름이 안정적입니다. |
| 방 생성/편집 | 중간 | 수동 생성과 메타데이터는 구현되어 있으나 자동 분할/병합은 보류입니다. |
| 문/창 opening | 높음 | 배치, marker, layout, 저장/로드, 테스트 근거가 있습니다. |
| 도면 오버레이 | 중간 | import/보정/표시는 가능하나 저장 정책과 bootstrap 분리가 필요합니다. |
| DWG/DXF import | 중간 | ACadSharp 기반 파싱과 scene apply는 있으나 검증/책임 분리 보강이 필요합니다. |
| 가구 배치/카탈로그 | 중간 | 기본 배치는 가능하나 외부 패치/동적 로더는 미완입니다. |
| 저장/로드 | 높음 | `.lhscene` DTO와 복원 테스트가 존재합니다. |
| Export | 중간 | legacy/extended DTO는 있으나 v2 business layer와 fixture 검증이 부족합니다. |
| UI/뷰 전환 | 높음 | 모드, 씬 계층, Top/3D 전환 테스트가 존재합니다. |
| FurnitureAuthoringTool | 낮음 | manifest UI는 있으나 build worker와 LH Editor 연동이 미완입니다. |

## 2. 벽 작성/편집

### 구현 내용

- `DrawManager`, `WallToolController`, `WallToolRuntime` 기반 벽 작성 흐름
- `Wall`, `WallData`, `WallObjectFactory` 기반 벽 오브젝트 생성
- `HandleManager` 기반 endpoint/split point 편집
- `SnapManager` 기반 grid, axis, handle, wall segment snap
- `WallSelectionManager` 기반 선택, 다중 선택, 이동
- `WallPropertyInputManager` 기반 길이/높이/두께/centerY 편집
- `UndoRedoManager` 기반 명령 이력

### 리스크

- 복잡한 코너의 3D miter mesh는 아직 후속 과제입니다.
- opening container와 일반 wall join 규칙을 더 명확히 검증해야 합니다.

## 3. 방 생성/편집

### 구현 내용

- `RoomCreateManager` 기반 사각형/폴리곤 방 생성
- `RoomManager` 기반 방 생성/삭제/목록/갱신
- `RoomPolygonValidationUtility` 기반 폴리곤 검증
- `RoomData` 기반 room code, native code, texture code 관리
- `RoomHandleManager` 기반 room polygon handle 편집
- `RoomGraphUtility`, `RoomPlanarGraph`, `VirtualBoundary` 기반 방/경계 계산 보조 구조

### 리스크

- 자동 room 추출, space cut/merge는 현재 주 흐름과 분리해야 합니다.
- room root 위치와 floor 시각 오브젝트 위치 정책을 문서화해야 합니다.

## 4. 도면 오버레이

### 구현 내용

- `DrawingOverlayImportController` 기반 이미지/PDF import
- `DrawingOverlayManager` 기반 문서 상태와 보정 흐름 관리
- `DrawingOverlayCalibrationService` 기반 보정 계산
- `DrawingOverlayRuntime` 기반 월드 좌표 표시
- `DrawingOverlayToolbarController` 기반 visible/lock/opacity 제어

### 리스크

- `.lhscene` 저장/로드에서 overlay를 복원하지 않습니다.
- PDF는 첫 페이지 중심입니다.
- `DrawingOverlaySceneBootstrap`에 UI 생성, PDF fallback, OS interop 책임이 몰려 있습니다.

## 5. DWG/DXF Import

### 구현 내용

- `CadWallImportService`가 ACadSharp로 CAD 파일을 읽고 벽 segment 후보를 생성합니다.
- `DwgWallImporter`가 파일 선택, 레이어/스케일 선택, import 옵션을 제어합니다.
- `DwgWallImportSceneApplier`가 segment를 실제 `Wall` GameObject로 변환합니다.
- `DwgWallImportSceneApplierTests`가 scene apply 일부를 검증합니다.

### 리스크

- 좌표계, 단위 스케일, 레이어 필터링 오류에 대한 사용자 피드백이 더 필요합니다.
- 중복/짧은 segment 제거 규칙을 fixture로 고정해야 합니다.
- import UI와 처리 로직의 책임 분리가 필요합니다.

## 6. 문/창 Opening

### 구현 내용

- `WallOpeningPlacementManager` 기반 문/창 배치
- `WallOpeningContainer`, `WallOpeningData`, `WallOpening` 기반 데이터와 오브젝트 관리
- `WallOpeningMarkerUI`, `WallOpeningMarkerDragController` 기반 marker 조작
- `DoorOpeningUIController`, `WindowOpeningUIController` 기반 타입별 UI
- 저장/로드와 export DTO에 opening 정보 포함

### 리스크

- opening layout 변경과 wall geometry 변경의 동기화 규칙을 더 많은 edge case로 검증해야 합니다.

## 7. 가구 배치/카탈로그

### 구현 내용

- `FurnitureCatalog` ScriptableObject 기반 항목 관리
- `FurnitureMenuController` 기반 메뉴 생성
- `FurniturePlacementManager` 기반 preview, 배치, 회전, 삭제
- `FurnitureInstance` 기반 code, nativeCode, defect tuple 보관

### 리스크

- 현재는 런타임 동적 패치 로더가 아니라 프로젝트 내 catalog/prefab 참조에 가깝습니다.
- FurnitureAuthoringTool manifest와 LH Editor runtime catalog의 연결이 미완입니다.
- 가구 충돌/방 귀속 검증은 export fixture로 보강해야 합니다.

## 8. 저장/로드

### 구현 내용

- `LhWorkStateSchema`의 DTO version 1 사용
- `LhWorkStateBuilder`가 벽, opening, 방, 가구를 DTO로 변환
- `LhWorkStateLoader`가 DTO를 scene object로 복원
- `LhWorkStatePersistenceController`가 저장/로드 UI 흐름 담당
- 관련 Editor 테스트가 다수 존재

### 리스크

- overlay가 저장 대상이 아니라서 사용자가 도면 보정 상태를 기대하면 불일치가 발생할 수 있습니다.
- 향후 schema version 2 도입 시 migration 정책이 필요합니다.

## 9. Export

### 구현 내용

- `LhSceneExporter`가 export 진입점 역할을 합니다.
- `LhSceneExportBuilder`가 wall/room/furniture DTO를 생성합니다.
- `LH.Schema`가 legacy/extended DTO를 정의합니다.
- room code, native code, floor/ceil, furniture defect tuple이 포함됩니다.

### 리스크

- 모바일 v2 문서의 `elements`, `defectCatalog`는 아직 구현되지 않았습니다.
- golden JSON fixture가 부족하면 schema drift를 조기에 잡기 어렵습니다.
- Export validation UI가 부족합니다.

## 10. UI/뷰 전환

### 구현 내용

- `ModeManager`, `ModeButtonBinder` 기반 모드 전환
- `EditorViewModeManager` 기반 Top/Perspective3D 전환
- `SceneHierarchyTreeModel`, `SceneHierarchyTreeView` 기반 씬 계층 표시
- Perspective highlight/framing 관련 controller와 테스트 존재

### 리스크

- 3D view의 역할을 inspection-only로 유지할지, 편집 기능을 확장할지 제품 정책이 필요합니다.

## 11. FurnitureAuthoringTool

### 구현 내용

- WPF 기반 manifest 생성/열기/저장/검증 UI
- item 추가/복제/삭제, defect 추가/삭제
- prefab/thumbnail source path 관리
- patch build 버튼의 초기 구조

### 미완 항목

- Unity Build Worker
- patch catalog 산출물 확정
- `FurnitureCatalog.asset` 자동 생성 흐름의 검증
- LH Editor 런타임 또는 Editor-time patch loader
