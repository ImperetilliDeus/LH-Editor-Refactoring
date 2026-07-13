# LH Editor Refactoring

Unity 기반 실내 공간 편집기 프로젝트의 리팩토링 버전 입니다. 
사용자는 도면 또는 CAD 자료를 기준으로 벽과 방을 만들고, 문/창/가구를 배치한 뒤 작업 상태를 저장하거나 모바일 뷰어용 JSON으로 내보낼 수 있습니다.

## 실행 환경

- Unity Editor: `6000.0.66f2`
- 주요 씬: `Assets/Scenes/SampleScene.unity`
- 스크립트 루트: `Assets/Scripts`
- 테스트 루트: `Assets/Tests/Editor`
- 외부 도구: `tools/FurnitureAuthoringTool`

## 핵심 편집 모드

| 모드 | 값 | 목적 |
|---|---:|---|
| `Default` | 0 | 벽 작성, 벽 endpoint 편집 |
| `RoomCreate` | 1 | 방 생성, 방 선택/이동/형상 편집 |
| `DetailEdit` | 3 | 벽 상세 편집, 다중 선택, 문/창 편집 |
| `DoorInsert` | 6 | 문 배치 |
| `WindowInsert` | 7 | 창 배치 |
| `FurniturePlace` | 9 | 가구 배치 |
| `DrawingOverlayCalibrate` | 10 | 도면 오버레이 보정 |

View 모드는 `Top`과 `Perspective3D`로 나뉩니다. 현재 3D View는 편집보다는 검토와 확인 중심으로 동작합니다.

## 주요 기능 상태

| 기능 | 상태 | 요약 |
|---|---|---|
| 벽 작성/편집 | 높음 | 생성, 선택, 이동, 길이/높이/두께 수정, 스냅, Undo/Redo, 문/창 opening 배치가 구현되어 있습니다. |
| 방 생성/편집 | 중간 | 사각형/폴리곤 기반 수동 생성, 메타데이터, 재질 코드, 벽 연결 정보를 다룹니다. 자동 분할/병합은 보류 성격입니다. |
| 도면 오버레이 | 중간 | PNG/JPG/PDF 첫 페이지를 불러와 보정하고 표시합니다. 보정 결과 저장/복원 정책은 확정이 필요합니다. |
| DWG/DXF 가져오기 | 중간 | ACadSharp 기반 레이어 선택과 벽 생성 흐름이 있습니다. 좌표/중복/스케일 검증 보강이 필요합니다. |
| 가구 배치 | 중간 | `FurnitureCatalog` 기반 프리팹 배치, 회전, 삭제, 방 귀속, defect tuple export가 가능합니다. 외부 패치 로더는 미완입니다. |
| 저장/로드 | 높음 | `.lhscene`으로 벽, opening, 방, 가구를 저장/복원합니다. Overlay는 현재 저장 대상이 아닙니다. |
| Export | 중간 | Legacy/Extended JSON DTO가 있습니다. `elements`, `defectCatalog`, golden fixture 검증은 후속 과제입니다. |
| UI/씬 계층/뷰 전환 | 높음 | 모드 버튼, 씬 계층 트리, Top/3D 전환, 3D 하이라이트/프레이밍 테스트가 존재합니다. |
| FurnitureAuthoringTool | 낮음 | manifest 작성/검증/WPF UI는 존재합니다. Unity Build Worker와 런타임 패치 로더가 핵심 미완 항목입니다. |

## 주요 문서

- [요구사항 정의서](docs/requirements.ko.md)
- [시스템 아키텍처 설계서](docs/architecture.md)
- [인터페이스 정의서](docs/interfaces.ko.md)
- [기능별 분석 및 완성도](docs/features.ko.md)
- [개선 방향 및 로드맵](docs/roadmap.ko.md)

## 코드 구조 요약

- `Assets/Scripts/Input`: Unity Input System을 편집 입력 프레임으로 변환합니다.
- `Assets/Scripts/Draw/Wall`: 벽 작성/편집, 핸들, 스냅, 문/창문(opening)을 담당합니다.
- `Assets/Scripts/Room`: 방 생성, 방 그래프, 폴리곤 검증, 방 메타데이터를 담당합니다.
- `Assets/Scripts/Overlay`: 이미지/PDF 도면 오버레이 import, 보정, 표시를 담당합니다.
- `Assets/Scripts/Import`: DWG/DXF 파싱과 벽 오브젝트 생성을 담당합니다.
- `Assets/Scripts/Furniture`: 가구 카탈로그, 배치, 인스턴스 메타데이터를 담당합니다.
- `Assets/Scripts/ProjectPersistence`: `.lhscene` 작업 상태 저장/로드를 담당합니다.
- `Assets/Scripts/Export`: 모바일 뷰어용 JSON export를 담당합니다.
- `tools/FurnitureAuthoringTool`: 가구 manifest 작성과 향후 패치 빌드 파이프라인을 담당합니다.

## 산출물 작성 기준

이 저장소의 장기 산출물은 한국어 문서를 기준으로 관리합니다. `docs/superpowers/specs`와 `docs/superpowers/plans`는 구현 전 설계 및 작업 계획 기록이며, 최종 산출물의 근거 자료로만 사용합니다.
