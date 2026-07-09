# 개선 방향 및 로드맵

## 1. 우선순위 요약

| 우선순위 | 과제 | 이유 |
|---|---|---|
| P0 | Export 계약 fixture와 validation | 모바일 연동 실패가 가장 큰 외부 리스크입니다. |
| P0 | FurnitureAuthoringTool build worker | 가구 추가 때마다 Editor를 재배포해야 하는 문제를 해결해야 합니다. |
| P0 | 저장/로드와 overlay 정책 확정 | 사용자가 작업 재개 시 도면 보정 상태를 기대할 수 있습니다. |
| P1 | Overlay/DWG importer 책임 분리 | 대형 bootstrap/importer는 유지보수 리스크가 큽니다. |
| P1 | Room 자동 기능 범위 정리 | 현재 메인 흐름과 보류 기능이 문서에 섞여 있습니다. |
| P1 | 3D wall join 검증 | 복잡한 코너에서 시각/geometry 품질 문제가 날 수 있습니다. |
| P2 | 문서 중복 정리 | 산출물의 장기 유지 비용을 줄입니다. |

## 2. P0 과제

### P0-01 Export 계약 고정

목표:

- legacy mobile viewer가 읽는 JSON 계약을 golden fixture로 고정합니다.
- Extended schema 변경 시 기존 legacy 계약이 깨지지 않게 합니다.

작업:

- 대표 평면 샘플을 기준으로 `wallData`, `roomData`, `furnish`, `defects` fixture 작성
- `LhSceneExportBuilderTests`에 golden JSON 비교 추가
- 누락 room code, native code, opening code, defect tuple validation 추가
- export 실패 메시지를 사용자에게 표시

완료 기준:

- legacy fixture와 extended fixture가 모두 테스트에서 통과합니다.
- 누락 필드가 있는 샘플은 명확한 validation 오류를 반환합니다.

### P0-02 FurnitureAuthoringTool Build Worker

목표:

- manifest를 LH Editor가 사용할 수 있는 patch 산출물로 변환합니다.

작업:

- patch output 구조 확정: `manifest.json`, `patch-catalog.json`, `build-report.txt`, `prefabs/`, `thumbnails/`
- Unity batchmode entry point 정리
- prefab/thumbnail 경로 검증
- `FurnitureCatalog.asset` 생성 자동화
- build report에 성공/실패/복사 파일 목록 기록

완료 기준:

- WPF 도구의 Patch Build가 실제 Unity batchmode를 실행합니다.
- 생성된 catalog를 LH Editor 프로젝트에서 열 수 있습니다.

### P0-03 저장/로드와 Overlay 정책 확정

목표:

- `.lhscene`이 무엇을 저장하는지 사용자 기대와 일치시킵니다.

선택지:

- A안: overlay는 작업 보조 자료로 보고 저장하지 않습니다. load 시 overlay clear를 명시합니다.
- B안: overlay source path, calibration, opacity, lock/visible 상태를 version 2 schema로 저장합니다.

권장:

- 단기에는 A안을 문서와 UI 문구로 명시합니다.
- 사용자가 도면 기반 작업 재개를 요구하면 B안을 `LhWorkStateDto` version 2로 설계합니다.

## 3. P1 과제

### P1-01 Overlay 책임 분리

문제:

- `DrawingOverlaySceneBootstrap`이 UI 생성, EventSystem, PDF fallback, native thumbnail, bootstrap 책임을 동시에 가집니다.

개선:

- PDF thumbnail loader 분리
- UI prefab/bootstrap 분리
- OS interop 분리
- runtime state와 view controller 분리

### P1-02 DWG Importer 책임 분리

문제:

- 파일 선택, layer popup, parse, apply, cleanup 정책이 한 흐름에 섞여 있습니다.

개선:

- `CadWallImportService`: 파일 파싱 전담
- `DwgWallImportPopupView`: 사용자 선택 전담
- `DwgWallImportExecutionBuilder`: import option 조립
- `DwgWallImportSceneApplier`: scene mutation 전담
- validation report DTO 추가

### P1-03 Room 자동 기능 범위 정리

문제:

- room graph, virtual boundary, space cut/merge 관련 문서가 현재 핵심 사용자 흐름과 섞여 있습니다.

개선:

- 수동 room 생성/편집을 현재 지원 기능으로 명시
- 자동 room 추출, 분할/병합은 보류 또는 실험 기능으로 표시
- 관련 문서를 `archive` 또는 `reference`로 분류

### P1-04 3D Wall Join 품질 보강

문제:

- 현재 3D wall join은 endpoint 보정 중심이며 복잡한 각도에서 miter mesh가 필요할 수 있습니다.

개선:

- 코너 유형별 샘플 fixture 생성
- opening container와 wall join 상호작용 검증
- 필요 시 wall body mesh를 endpoint별 가변 면 구조로 전환

## 4. P2 과제

### P2-01 문서 체계 정리

권장 구조:

- `README.md`: 프로젝트 개요와 문서 인덱스
- `docs/requirements.ko.md`: 요구사항 정의서
- `docs/architecture.md`: 시스템 아키텍처 설계서
- `docs/interfaces.ko.md`: 인터페이스 정의서
- `docs/features.ko.md`: 기능별 분석
- `docs/roadmap.ko.md`: 개선 방향
- `docs/archive/`: 과거 설계 메모, 보류 기능, superpowers 실행 기록의 요약본

병합 후보:

- `docs/next-steps.md`와 `docs/code-reduction-roadmap.md`
- `docs/inspector-mapping.md`, `docs/direct-connection-checklist.md`, `docs/scenes-and-references.md`

### P2-02 테스트 명명/fixture 정리

- 기능별 fixture 폴더를 만들고 export/load/import 샘플을 분리합니다.
- 깨지기 쉬운 씬 의존 테스트는 pure DTO/서비스 테스트와 분리합니다.

## 5. 보류 또는 제외 항목

- 완전 자동 room split/merge
- 모바일 뷰어의 defect selection runtime 구현
- Addressables/AssetBundle 기반 완전 동적 가구 로딩
- 3D view에서의 직접 편집 기능

이 항목들은 현재 산출물에서는 요구사항으로 확정하지 않고, 별도 승인된 설계가 생길 때 범위에 포함합니다.
