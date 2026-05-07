# FurnitureAuthoringTool

`FurnitureAuthoringTool`은 가구 프리팹 패치 패키지를 저작하기 위한 별도 도구입니다.

1차 목표는 UI 기능 구현보다 먼저 `manifest` 데이터 계약을 고정하는 것입니다.

현재 기준 문서:

- [Manifest Schema](./docs/manifest-schema.md)
- [Sample Manifest](./samples/furniture-manifest.sample.json)

이 계약을 기준으로 다음 단계를 진행합니다.

1. JSON 저장/불러오기
2. 검증 로직
3. WPF 편집 화면
4. 1차 Patch Build Worker 연동

현재 `Patch Build`는 아래 순서로 동작합니다.

1. manifest 검증
2. 로컬 패치 산출물 폴더 생성
3. Unity `-batchmode` 실행
4. Unity 프로젝트 내 `FurnitureCatalog.asset` 생성

로컬 산출물은 아래 파일을 포함합니다.

- `manifest.json`
- `patch-catalog.json`
- `build-report.txt`
- 복사된 `prefabs/`
- 복사된 `thumbnails/`

Unity import 결과물은 기본적으로 아래 경로에 생성됩니다.

- `Assets/Generated/FurniturePatches/<catalogVersion_timestamp>/FurnitureCatalog.asset`
- `Assets/Generated/FurniturePatches/<catalogVersion_timestamp>/Prefabs/*`
- `Assets/Generated/FurniturePatches/<catalogVersion_timestamp>/Thumbnails/*`

상세 구조:

- [Manifest Schema](./docs/manifest-schema.md)
- [Patch Catalog Schema](./docs/patch-catalog-schema.md)

## 직접 해야 하는 설정

처음 1회는 WPF 도구에서 `Unity Editor 실행 파일` 경로를 지정해야 합니다.

예시:

- `C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe`

`Unity 프로젝트 경로`는 보통 자동 감지되지만, 비어 있거나 잘못 잡히면 이 저장소 루트로 수정합니다.

예시:

- `E:\Unity\LH Editor_Refactoring`
