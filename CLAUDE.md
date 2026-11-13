# LH Editor Refactoring

도면·CAD 자료를 기준으로 벽과 방을 만들고 문·창·가구를 배치한 뒤, 작업 상태(`.lhscene`)를 저장하거나 모바일 뷰어용 JSON으로 내보내는 Unity 실내 공간 편집기다.
작업 전 개요는 [README.md](README.md), 구조는 [docs/architecture.md](docs/architecture.md), 구현 규칙은 [docs/conventions.md](docs/conventions.md)를 본다.

## 모델 역할 분담

- 설계·사고·분석은 Opus가 맡는다. 비자명한 작업은 Plan 모드에서 설계를 확정한 뒤 실행한다 (`opusplan`: Plan 모드는 Opus, 실행은 Sonnet).
- 코드 탐색·검색은 `Explore` 서브에이전트(Sonnet)에 맡긴다.
- 설계가 확정된 구현은 서브에이전트에 넘길 수 있다. 넘길 때 변경 파일, 동작, 완료 기준을 명시한다.

## 구조

| 위치 | 역할 |
|---|---|
| `Assets/Scenes/SampleScene.unity` | 주 편집 씬 |
| `Assets/Scripts/ModeManager.cs` | 편집 모드 상태(`EditorMode`)만 관리 |
| `Assets/Scripts/Common` | 기능 공용 geometry 유틸 (asmdef `LH.Common`, 테스트가 직접 참조). 다각형·Bounds XZ 판정, 허용 오차 상수 |
| `Assets/Scripts/Input` | Unity Input System 입력을 편집 입력 프레임으로 변환 |
| `Assets/Scripts/Draw/Wall` | 벽 작성·편집, 핸들, 스냅, opening(문·창) |
| `Assets/Scripts/Room` | 방 생성, 방 그래프, 폴리곤 검증, 메타데이터 |
| `Assets/Scripts/Overlay` | 이미지·PDF 도면 오버레이 import·보정·표시 |
| `Assets/Scripts/Import` | DWG/DXF 파싱과 벽 생성 (ACadSharp) |
| `Assets/Scripts/Furniture` | 가구 카탈로그·배치·인스턴스 메타데이터 |
| `Assets/Scripts/ProjectPersistence` | `.lhscene` 작업 상태 저장·로드 |
| `Assets/Scripts/Export` | 모바일 뷰어용 JSON export |
| `Assets/Editor` | 프리팹 생성기 등 Editor 전용 도구 |
| `Assets/Tests/Editor` | EditMode 테스트 (asmdef `LH.Editor.Tests`, 고정 데이터는 `Fixtures`) |
| `Assets/ThirdParty` | ACadSharp, CSUtilities, Paroxe PDFRenderer 등 외부 라이브러리 |
| `tools/FurnitureAuthoringTool` | 가구 manifest 작성 WPF 도구 (자체 sln·tests) |
| `docs/` | 요구사항·아키텍처·인터페이스·스키마 문서. `docs/superpowers`, `.agents`는 설계·계획 기록 |

- Unity `6000.0.66f2`, URP 17.0.4, Input System 1.17.0, Unity Test Framework 1.6.0.
- `WorkStates/`, `_library_backups/`, `.worktrees/`, `.superpowers/`는 Git 제외 대상인 로컬 작업물이다.

## 실행·검증

| 목적 | 방법 |
|---|---|
| Editor 실행 | Unity Hub에서 `6000.0.66f2`로 열고 `Assets/Scenes/SampleScene.unity` Play |
| 빠른 컴파일 확인 | `dotnet build Assembly-CSharp.csproj -nologo` (csproj는 Unity가 생성하므로 Editor에서 한 번 연 뒤 사용) |
| EditMode 테스트 (GUI) | Window > General > Test Runner > EditMode > Run All |
| EditMode 테스트 (CLI, Editor를 닫은 상태) | `& "C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs\EditModeResults.xml -logFile Logs\EditModeTests.log` |
| FurnitureAuthoringTool | `tools/FurnitureAuthoringTool/README.md` 참고 |

- 같은 프로젝트를 Editor로 열어 둔 채 batchmode를 실행하면 잠금 때문에 실패한다.
- 작업을 마치면 컴파일 결과(오류·경고 수)와 관련 EditMode 테스트 결과를 보고한다. 실행하지 못한 검증은 하지 못했다고 명시한다.

## 코딩 규칙

기존 코드의 관례를 따른다. 주변 파일과 다르게 쓰지 않는다.

- 여는 중괄호는 다음 줄(Allman), 들여쓰기 4칸(JSON·YAML 2칸). 기존 파일의 줄끝과 인코딩을 유지하고, 새 파일은 LF·UTF-8로 만든다.
- 이름: private 필드·`[SerializeField]` 필드는 접두사 없는 camelCase(`registeredButtons`, `initialMode`), 타입·메서드·속성·이벤트는 PascalCase, 상수는 PascalCase. 문자열 비교는 리터럴을 왼쪽에 둔다.
- 코드 주석은 주변 파일의 언어를 따른다(현재 코드 주석은 대부분 영어). 이유가 비자명한 곳에만 단다.
- 단계 번호(Step 1, 2…) 같은 작업 이력성 주석은 넣지 않는다.
- 책임 분리와 이벤트 규칙은 [docs/conventions.md](docs/conventions.md)를 따른다: geometry 계산과 UI 반영 분리, 구독은 `Awake`·초기화에서 하고 해제는 `OnDestroy`에서, 큰 갱신은 dirty flag·`MarkDirty()` 우선.

## 문서

- 장기 산출물 문서는 한국어로 쓴다. `docs/superpowers`의 specs·plans는 근거 자료일 뿐 최종 산출물이 아니다.
- 사용자 체감 변경이 있으면 최소한 `README.md`, `docs/workflows.md`, `docs/operations.md`를 함께 갱신한다.
- 저장(`.lhscene`)·export 스키마를 바꾸면 `docs/work-state-save-load.md`, `docs/export.md`, `docs/mobile-viewer-schema-v2*.md`도 맞춘다.

## Unity 작업 시 주의

- 에셋을 만들거나 옮기거나 지울 때 `.meta` 파일도 함께 처리한다. 기존 `.meta`의 GUID를 바꾸면 씬·프리팹 참조가 끊어진다.
- 씬(`.unity`)·프리팹(`.prefab`)·에셋 YAML은 직접 편집을 최소화하고, 필요하면 변경 내용을 설명해 Editor에서 확인하도록 안내한다.
- `Library/`, `Temp/`, `Logs/`, `*.csproj`, `*.slnx` 등 Unity가 생성하는 파일은 수정하지 않는다.
- 작업 트리에 사용자가 작업 중인 미커밋 변경이 있을 수 있다. 맡은 범위 밖의 변경은 되돌리거나 덮어쓰지 않는다.

## 하지 말 것

- `Assets/ThirdParty`(ACadSharp, CSUtilities, Paroxe, Plugins)와 `Assets/TextMesh Pro`는 수정하지 않는다. 필요하면 변경 요청 사항으로 보고한다.
- `.lhscene` 저장 형식과 모바일 뷰어 export JSON 계약을 하위 호환 없이 바꾸지 않는다. 기존 작업 파일과 모바일 뷰어가 읽을 수 있어야 하며, 바꿔야 하면 먼저 보고한다.
- 뷰어 쪽 프리팹이 참조하는 스크립트의 `.meta` GUID를 바꾸지 않는다.
- 비밀값(서버 인증 정보, 토큰 등)을 소스·로그·Git에 남기지 않는다.
