# 요구사항 정의서

## 1. 목적

본 문서는 LH Editor Refactoring 프로젝트의 기능 요구사항과 비기능 요구사항을 정리합니다. 산출물 작성 기준일은 2026-07-09이며, 현재 코드와 문서를 기준으로 구현된 범위와 보류 범위를 구분합니다.

## 2. 범위

### 포함 범위

- 도면 오버레이 import 및 보정
- DWG/DXF 기반 벽 가져오기
- 벽 작성/편집, 문/창 opening 배치
- 방 생성/편집, 방 메타데이터 관리
- 가구 카탈로그 기반 배치
- 작업 상태 저장/로드
- 모바일 뷰어용 JSON export
- Top/Perspective view 전환 및 씬 계층 UI
- FurnitureAuthoringTool 기반 가구 manifest 작성

### 제외 또는 보류 범위

- 완전 자동 방 분할/병합
- 모바일 뷰어 자체 구현
- Export v2의 `elements`, `defectCatalog` 완전 구현
- Furniture patch 런타임 동적 로더
- 복잡한 3D wall miter mesh 자동 보정

## 3. 사용자와 시나리오

| 사용자 | 목표 |
|---|---|
| 공간 편집 작업자 | 도면 또는 CAD를 기준으로 방과 벽을 빠르게 구성하고 문/창/가구를 배치합니다. |
| 품질 검수자 | Top/3D view와 씬 계층 UI로 편집 결과를 확인합니다. |
| 모바일 연동 개발자 | legacy viewer 호환 JSON과 향후 v2 schema 확장 지점을 확인합니다. |
| 가구 데이터 관리자 | FurnitureAuthoringTool로 가구 manifest와 defect tuple을 관리합니다. |

## 4. 기능 요구사항

### FR-01 도면 오버레이

- 사용자는 PNG/JPG/PDF 도면을 불러올 수 있어야 합니다.
- PDF는 최소한 첫 페이지를 텍스처로 표시할 수 있어야 합니다.
- 사용자는 두 기준점의 실제 거리, 원점, 회전을 이용해 도면을 Top view에 보정할 수 있어야 합니다.
- 사용자는 overlay의 표시 여부, 잠금 여부, 투명도를 조정할 수 있어야 합니다.
- 현재 요구사항 결정 필요: overlay 보정 결과를 `.lhscene`에 저장할지 여부.

### FR-02 DWG/DXF 가져오기

- 사용자는 DWG/DXF 파일을 선택할 수 있어야 합니다.
- 시스템은 CAD 레이어 목록을 추출하고 사용자가 가져올 레이어를 선택할 수 있어야 합니다.
- 시스템은 `Line`, `LwPolyline`, `Polyline2D` 기반 벽 후보를 벽 오브젝트로 변환해야 합니다.
- 사용자는 import 시 기존 벽/방 삭제 여부와 단위 스케일을 선택할 수 있어야 합니다.
- import 실패 시 기존 씬 상태는 손상되지 않아야 합니다.

### FR-03 벽 작성/편집

- 사용자는 Top view에서 벽을 작성할 수 있어야 합니다.
- 사용자는 벽 endpoint handle을 이동할 수 있어야 합니다.
- 시스템은 grid, axis, handle, wall segment snap을 제공해야 합니다.
- 사용자는 벽을 선택, 다중 선택, 이동, 삭제할 수 있어야 합니다.
- 사용자는 길이, 높이, 두께, centerY 등 벽 속성을 편집할 수 있어야 합니다.
- 벽 생성/이동/opening 변경은 Undo/Redo 대상이어야 합니다.

### FR-04 문/창 opening

- 사용자는 선택한 벽에 문 또는 창을 배치할 수 있어야 합니다.
- opening은 벽 segment 기준 위치, 폭, 높이, 깊이, 하단 높이를 가져야 합니다.
- 사용자는 opening marker 또는 container를 통해 위치를 조정할 수 있어야 합니다.
- opening은 저장/로드와 export에 포함되어야 합니다.

### FR-05 방 생성/편집

- 사용자는 사각형 또는 폴리곤 방식으로 방을 생성할 수 있어야 합니다.
- 시스템은 방 폴리곤이 유효한지 검증해야 합니다.
- 사용자는 방 이름, room type key, room code, native code를 관리할 수 있어야 합니다.
- 사용자는 floor/ceiling texture code를 지정할 수 있어야 합니다.
- 방은 연결된 벽 id 목록과 수동 벽 선택 정보를 보존해야 합니다.

### FR-06 가구 배치

- 사용자는 `FurnitureCatalog`에서 가구를 선택할 수 있어야 합니다.
- 시스템은 선택된 가구 프리팹을 프리뷰로 표시하고 배치 가능한 위치를 검증해야 합니다.
- 사용자는 가구를 배치, 회전, 삭제할 수 있어야 합니다.
- 가구 인스턴스는 catalog code, export code, native code, defect tuple을 보존해야 합니다.
- 가구는 가능하면 방에 귀속되어 export되어야 합니다.

### FR-07 저장/로드

- 사용자는 현재 작업 상태를 `.lhscene` JSON으로 저장할 수 있어야 합니다.
- 저장 대상은 벽, opening, 방, 가구입니다.
- 사용자는 `.lhscene`을 불러와 작업 상태를 복원할 수 있어야 합니다.
- 로드 실패 시 기존 씬 상태는 변경되지 않아야 합니다.
- 지원하지 않는 schema version은 명확한 오류로 거부해야 합니다.

### FR-08 Export

- 사용자는 모바일 뷰어 호환 JSON을 생성할 수 있어야 합니다.
- export는 wallData, roomData, startPoint를 포함해야 합니다.
- Extended export는 room id, roomTypeKey, nativeCode, 가구 name/nativeCode 등 확장 정보를 포함할 수 있어야 합니다.
- Export 전 누락 code, 빈 geometry, 잘못된 prefab/reference를 검증해야 합니다.
- 향후 v2에서는 `elements`와 `defectCatalog`를 병행 export해야 합니다.

### FR-09 View/UI

- 사용자는 Top view와 Perspective3D view를 전환할 수 있어야 합니다.
- 3D view 전환 시 Top view 전용 root는 비활성화되고, 복귀 시 이전 활성 상태가 복원되어야 합니다.
- 씬 계층 트리는 벽, 방, opening, 가구를 탐색할 수 있어야 합니다.
- 모드 버튼은 현재 모드와 상호작용 가능 상태를 동기화해야 합니다.

### FR-10 FurnitureAuthoringTool

- 사용자는 별도 WPF 도구에서 가구 manifest를 작성/수정/저장할 수 있어야 합니다.
- manifest는 code, displayName, exportCode, nativeCode, prefabSourcePath, thumbnailSourcePath, transform 기본값, defect tuple을 포함해야 합니다.
- 도구는 manifest validation을 수행해야 합니다.
- 후속 구현은 Unity Build Worker로 patch catalog, prefab, thumbnail, `FurnitureCatalog.asset` 산출물을 만들어야 합니다.

## 5. 비기능 요구사항

| ID | 요구사항 |
|---|---|
| NFR-01 | 저장/로드/import/export 실패는 기존 작업 상태를 보존해야 합니다. |
| NFR-02 | Export schema는 version과 golden fixture 회귀 테스트로 보호해야 합니다. |
| NFR-03 | 내부 편집 모델과 모바일 viewer business schema는 adapter로 분리해야 합니다. |
| NFR-04 | 대형 manager/bootstrap 클래스는 UI, 파일 I/O, 파싱, scene apply 책임을 분리해야 합니다. |
| NFR-05 | 기능별 핵심 DTO와 알고리즘은 Editor 테스트로 검증해야 합니다. |
| NFR-06 | Top view와 3D view는 동일한 씬 상태를 공유해야 하며, 3D view는 현재 inspection 중심으로 유지합니다. |
| NFR-07 | 문서는 한국어 산출물을 기준으로 유지하고, 구현 상태를 구현/부분 구현/보류로 명시해야 합니다. |

## 6. 수용 기준

- `Assets/Tests/Editor`의 저장/로드, export, wall opening, room, view mode 관련 테스트가 통과해야 합니다.
- 신규 export 변경은 legacy fixture와 extended fixture를 모두 통과해야 합니다.
- `.lhscene` 로드 실패 케이스는 현재 씬 보존을 검증해야 합니다.
- Furniture patch 기능을 완성할 때는 manifest validation, batch build, LH Editor catalog load가 모두 자동 테스트 또는 수동 체크리스트로 검증되어야 합니다.
