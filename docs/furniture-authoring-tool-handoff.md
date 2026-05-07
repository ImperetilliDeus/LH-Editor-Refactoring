# Furniture Authoring Tool Handoff

## 목적

이 문서는 `LH Editor_Refactoring` 프로젝트에서 논의한 `FurnitureAuthoringTool` 관련 배경, 요구사항, 현재 구현 상태를 다른 채팅이나 다른 작업 세션으로 옮기기 위한 인수인계 문서다.

---

## 배경

기존 Unity 프로젝트에는 `FurnitureCatalogBuilder.cs`가 있으며, Unity Editor 내에서 가구 프리팹을 스캔해서 카탈로그와 썸네일을 만들고, 이를 기반으로 가구 배치용 버튼과 이미지를 구성하고 있었다.

문제는 새 아파트 단지 추가 시 가구가 계속 늘어나고, 그때마다 `LH Editor` 자체를 다시 빌드해서 새 exe를 배포해야 할 가능성이 높다는 점이었다.

초기에는 `Addressables` 또는 `AssetBundle` 기반으로 `가구 프리팹 패치 시스템`을 만들자는 방향을 검토했다.

이후 요구사항이 더 구체화되면서, 단순히 가구 에셋만 추가하는 게 아니라 가구별로 `Furnish data`도 입력해야 한다는 점이 확인되었다.

---

## Furnish 관련 핵심 정리

참고 JSON:

- `e:\Unity\LHM_260212\Assets\Resources\양산21A_250320.json`

이 JSON을 기준으로 정리된 내용은 아래와 같다.

1. `furnish[].code`
- Unity Editor 내 furnish preset 이름과 연결되는 식별자

2. `furnish[].position`
3. `furnish[].angle`
4. `furnish[].scale`
- 실제 scene 배치 결과로 결정되는 값
- 별도 저작 도구에서 직접 입력하는 대상이 아님

5. `furnish[].defects`
- 외부 저작 도구에서 입력해야 하는 핵심 데이터
- 구조:

```json
"defects": [
  {
    "mntnCd": "901",
    "locCd": "2",
    "mtrlCd": "080"
  }
]
```

정리하면:

- `position / angle / scale`은 배치 결과
- `defects`는 가구 정의(master data)에 포함되어야 하는 값

---

## 구조 결정

최종적으로 다음 방향이 더 적절하다고 판단했다.

### 하지 않기로 한 것

- 기존 `LH Editor` 프로젝트 안에 저작 도구를 억지로 계속 확장하는 것
- 일반 exe 하나가 Unity import, prefab 처리, thumbnail 생성, bundle 빌드까지 전부 직접 하는 것

### 하기로 한 것

별도 저작 도구 프로젝트 `FurnitureAuthoringTool`을 만든다.

이 도구의 책임:

- 가구 정의 입력
- `code`, `displayName`, `exportCode`, `nativeCode` 관리
- `prefabSourcePath`, `thumbnailSourcePath` 관리
- `placementOffset`, `defaultEulerAngles`, `boundsSize` 관리
- `defects[]` 관리
- JSON manifest 저장

이후 별도 작업으로 `Unity Build Worker`를 만들어서:

- manifest JSON을 읽고
- 실제 패치 산출물로 변환하고
- LH Editor가 읽을 수 있는 런타임 카탈로그/에셋 패키지를 생성해야 한다

즉 현재 저장되는 JSON은 최종 런타임 패치 파일이 아니라 `저작 원본(manifest)`이다.

---

## 새 프로젝트 생성 위치

현재 새 프로젝트는 기존 저장소 내부에 생성됨:

- `tools/FurnitureAuthoringTool`

솔루션:

- [FurnitureAuthoringTool.sln](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/FurnitureAuthoringTool.sln:1)

프로젝트 구성:

- `FurnitureAuthoring.Tool`
- `FurnitureAuthoring.Contracts`
- `FurnitureAuthoring.Domain`
- `FurnitureAuthoring.Application`
- `FurnitureAuthoring.Infrastructure`

다만 실제 동작 안정성을 위해 현재 WPF 실행 프로젝트는 `Tool -> Contracts`만 참조하도록 단순화해 둔 상태다.

---

## 현재 구현된 것

### 1. 데이터 계약 문서화

문서:

- [README.md](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/README.md:1)
- [manifest-schema.md](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/docs/manifest-schema.md:1)
- [furniture-manifest.sample.json](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/samples/furniture-manifest.sample.json:1)

manifest 핵심 필드:

- `manifestVersion`
- `catalogVersion`
- `createdAt`
- `author`
- `items[]`

item 핵심 필드:

- `code`
- `displayName`
- `exportCode`
- `nativeCode`
- `prefabSourcePath`
- `thumbnailSourcePath`
- `placementOffset`
- `defaultEulerAngles`
- `boundsSize`
- `defects[]`

### 2. DTO / 모델

위치:

- [FurnitureManifestDto.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Contracts/Models/FurnitureManifestDto.cs:1)
- [FurnitureItemDto.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Contracts/Models/FurnitureItemDto.cs:1)
- [FurnitureDefectDto.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Contracts/Models/FurnitureDefectDto.cs:1)
- [Vector3Value.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Contracts/Models/Vector3Value.cs:1)

현재 WPF 바인딩을 위해 `INotifyPropertyChanged` 지원이 들어가 있다.

### 3. WPF UI

위치:

- [MainWindow.xaml](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/MainWindow.xaml:1)
- [MainWindow.xaml.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/MainWindow.xaml.cs:1)
- [WindowViewModel.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/ViewModels/WindowViewModel.cs:1)

구현된 기능:

- 새 매니페스트 생성
- 샘플 불러오기
- JSON 열기
- 저장
- 다른 이름으로 저장
- 검증
- 패치 빌드
- 가구 항목 추가
- 가구 항목 복제
- 가구 항목 삭제
- defect 행 추가
- defect 행 삭제
- prefab 경로 선택
- thumbnail 경로 선택

### 4. 저장 및 검증

위치:

- [JsonFurnitureManifestStore.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/Services/JsonFurnitureManifestStore.cs:1)
- [FurnitureManifestValidator.cs](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/Services/FurnitureManifestValidator.cs:1)

검증 규칙:

- `manifestVersion > 0`
- `catalogVersion` 필수
- `author` 필수
- 각 item의 `code` 필수
- `code` 중복 금지
- `displayName` 필수
- `exportCode` 필수
- `prefabSourcePath` 필수
- defect의 `mntnCd / locCd / mtrlCd` 공백 금지

### 5. 한글 UI 반영

요청에 따라 UI와 메시지 문구는 전부 한글로 변경했다.

---

## 현재 빌드/실행 방법

작업 루트:

- `E:\Unity\LH Editor_Refactoring`

빌드:

```powershell
dotnet build "tools\FurnitureAuthoringTool\src\FurnitureAuthoring.Tool\FurnitureAuthoring.Tool.csproj"
```

실행:

```powershell
.\tools\FurnitureAuthoringTool\src\FurnitureAuthoring.Tool\bin\Debug\net7.0-windows\FurnitureAuthoring.Tool.exe
```

실행 파일:

- [FurnitureAuthoring.Tool.exe](E:/Unity/LH%20Editor_Refactoring/tools/FurnitureAuthoringTool/src/FurnitureAuthoring.Tool/bin/Debug/net7.0-windows/FurnitureAuthoring.Tool.exe:1)

---

## 저장되는 JSON의 의미

현재 `저장` 버튼으로 생성되는 JSON은 바로 LH Editor가 읽는 최종 패치 파일이 아니다.

현재 의미:

- 사람이 편집한 가구 메타데이터 원본
- `Unity Build Worker`의 입력값

즉 현재 단계:

1. FurnitureAuthoringTool에서 manifest 저장
2. 이후 별도 구현될 Unity Build Worker가 이 manifest를 읽어
3. 실제 패치 산출물로 변환해야 함

---

## 아직 남은 작업

### 최우선

1. `Unity Build Worker` 1차 구현
- manifest 읽기
- prefab/thumbnail 경로 검증
- 패치 출력용 구조 생성
- 런타임 카탈로그 생성

2. `Patch Build` 버튼 실제 연동
- 현재는 검증 후 manifest를 출력 폴더에 저장하는 수준
- 나중에는 Unity batchmode 또는 별도 워커 호출로 연결해야 함

### 이후

3. LH Editor 런타임 로더 구현
- 기존 `FurnitureCatalog` 직접 참조 제거
- 외부 카탈로그 로드
- 메뉴 생성
- prefab 로드
- 배치 시 defects 반영

---

## 현재 대화에서 중요한 결론

1. `FurnitureAuthoringTool`은 별도 프로젝트로 가는 것이 맞다.
2. `position / angle / scale`은 저작 도구의 입력 대상이 아니다.
3. `defects`는 가구 정의의 일부이며, 배치 결과에 복사되어 export되어야 한다.
4. 지금 저장되는 JSON은 저작 원본(manifest)이다.
5. 다음 핵심 단계는 `Unity Build Worker` 구현이다.

---

## 다음 채팅에서 이어가기 좋은 요청 예시

다음 채팅으로 옮길 때는 아래처럼 요청하면 이어서 작업하기 쉽다.

### 예시 1

`docs/furniture-authoring-tool-handoff.md를 기준으로 FurnitureAuthoringTool의 다음 단계인 Unity Build Worker를 설계해줘.`

### 예시 2

`FurnitureAuthoringTool은 현재 manifest 저장까지만 된다. docs/furniture-authoring-tool-handoff.md를 읽고 Patch Build 버튼을 실제 Unity batchmode 연동으로 바꿔줘.`

### 예시 3

`FurnitureAuthoringTool의 현재 UI를 기준으로 defects 편집 UX를 개선해줘. handoff 문서를 먼저 참고해줘.`

