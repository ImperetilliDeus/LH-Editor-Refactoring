# Conventions

## 목적

현재 코드베이스에서 유지하면 좋은 구현 규칙과 문서 규칙을 정리합니다.

## 코드 구조

- 기능 단위로 폴더를 나눕니다.
- 큰 매니저는 partial class로 분리할 수 있습니다.
- geometry 계산과 UI 반영 로직은 분리합니다.
- `SerializeField` 참조와 런타임 캐시는 구분해서 둡니다.

## 책임 분리

- `ModeManager`는 모드 상태만 관리합니다.
- 입력 해석은 해당 도메인 매니저에서 처리합니다.
- top view 렌더링은 데이터 소유 로직과 분리합니다.
- room/wall/opening 데이터와 실제 표시 계층은 가능한 한 느슨하게 연결합니다.

## 이벤트와 갱신

- 이벤트 구독은 `Awake` 또는 초기화 루틴에서 수행합니다.
- 해제는 `OnDestroy`에서 수행합니다.
- 큰 화면 갱신은 직접 즉시 렌더링하기보다 dirty flag 또는 명시적 `MarkDirty()`를 우선합니다.
- 단, 사용자 입력 직후 화면 반영이 필요한 편집은 즉시 refresh를 허용합니다.

## wall / handle 규칙

- wall 편집 로직은 `StartPoint`, `EndPoint`, vertex id를 기준으로 유지합니다.
- handle snap modifier와 grid snap modifier는 별도 정책으로 관리합니다.
- split point는 일반 endpoint와 다르게 슬라이드 제약이 있을 수 있으므로 별도 플래그를 유지합니다.
- 3D wall join 보정은 시각 보정이어야 하며, 기본 endpoint 데이터는 가능하면 훼손하지 않습니다.

## room 규칙

- room polygon은 sanitize 후 사용합니다.
- room floor mesh는 polygon triangulation 결과를 사용합니다.
- floor 시각 오브젝트의 월드 높이 정책이 있으면 문서와 코드에 함께 반영합니다.

## 문서 규칙

- `README.md`는 프로젝트 개요와 현재 편집 흐름을 설명합니다.
- 세부 동작은 `docs/*.md`로 분리합니다.
- 사용자 체감 변경이 있으면 최소한 `README.md`, `docs/workflows.md`, `docs/operations.md`를 함께 갱신합니다.
