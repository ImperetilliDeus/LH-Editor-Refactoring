# B001 Parametric Opening 변경 기록

- 작성일시: 2026-07-14 14:25:06 +09:00
- 대상 1: `E:\Unity\LH Editor_Refactoring`
- 대상 2: `E:\Unity\LHM_260212`
- 목적: 베란다형 창호 `B001`이 opening의 가로, 세로, 깊이 변경 시 전체 prefab non-uniform scale로 찌그러지지 않고, 고정 파트와 늘어나는 파트를 분리한 parametric 규약으로 동작하도록 Editor와 Viewer 양쪽을 맞춘다.

## 공통 적용 규약

### 모델 축 규약

Unity에서 사용할 최종 prefab은 다음 좌표 규약을 따른다.

- `X`: opening 가로 폭, width
- `Y`: opening 세로 높이, height
- `Z`: opening 깊이, depth
- prefab root scale은 기본적으로 `(1, 1, 1)`을 유지한다.
- opening 크기 변경을 prefab root의 non-uniform scale로 해결하지 않는다.
- Blender/FBX import 과정에서 축 보정 회전이 prefab variant root에 남지 않도록 `bakeAxisConversion`을 사용한다.

### Parametric Part Naming 규약

모델 내부 part 이름은 런타임 리사이즈 규칙을 해석할 수 있도록 prefix를 가진다.

- `Fixed_`: 치수 자체는 유지하고 위치만 opening 크기에 맞게 재배치하는 파트
- `Stretch_`: 특정 축으로만 늘어나는 파트

예시:

- `Fixed_BalconyFrame_Left`
- `Fixed_BalconyFrame_Right`
- `Fixed_Center_Mullion`
- `Fixed_Railing_Vertical_Bar_01`
- `Stretch_Glass_Left_SlidingPanel`
- `Stretch_Glass_Right_SlidingPanel`
- `Stretch_Railing_Top_Rail`
- `Stretch_Railing_Mid_Rail`
- `Stretch_Railing_Bottom_Rail`

### B001 Parametric Profile 규약

`B001`은 일반 window prefab과 다르게 `BALCONY_RAILING_WINDOW_V1` profile을 사용한다.

- `parametricProfileKey`: `BALCONY_RAILING_WINDOW_V1`
- `authoredSize`: `(1.8, 2.1, 0.14)`
- 기준 단위: meter
- catalog fit 정책: `fitWidth=false`, `fitHeight=false`, `fitDepth=false`
- 의미: catalog의 fit flag로 prefab 전체를 늘리지 않고, `ParametricOpeningModel`이 내부 part만 재배치/리사이즈한다.

### JSON Export 규약

기존 Viewer 호환성을 위해 `door/window`의 기존 필드는 유지한다.

- `isExist`
- `code`
- `position`
- `angle`
- `scale`

Parametric opening 처리를 위해 아래 필드를 추가한다.

- `parametricProfileKey`
- `authoredSize`
- `width`
- `height`
- `depth`
- `bottomY`

Viewer는 `parametricProfileKey`가 있거나 `code == "B001"`인 window를 parametric window로 해석한다. 이 경우 `scale`만으로 prefab 전체를 늘리지 않고, `width/height/depth`를 기준으로 내부 part를 조정한다.

## LH Editor_Refactoring 변경사항

### OpeningTypeCatalog 규약 확장

파일:

- `Assets/Scripts/Draw/Wall/Opening/OpeningTypeCatalog.cs`
- `Assets/Resources/OpeningTypeCatalog.asset`

변경 내용:

- `OpeningTypeCatalogItem`에 parametric 메타데이터를 추가했다.
- 추가 필드:
  - `useParametricModel`
  - `parametricProfileKey`
  - `authoredSize`
- `B001` catalog 항목을 window 타입으로 등록했다.
- `B001` 항목은 다음 값을 가진다.
  - `typeKey: B001`
  - `displayName: 베란다(난간형)`
  - `parametricProfileKey: BALCONY_RAILING_WINDOW_V1`
  - `authoredSize: {x: 1.8, y: 2.1, z: 0.14}`
  - `referenceSize: {x: 1.8, y: 2.1, z: 0.14}`
  - `fitDepth: 0`
  - `fitHeight: 0`
  - `fitWidth: 0`

적용 규약:

- 일반 door/window는 기존 fit flag 기반 scale 정책을 유지한다.
- `useParametricModel`이 켜진 opening은 catalog fit flag로 전체 모델을 맞추지 않는다.
- `authoredSize`는 모델이 제작된 기준 크기이며, runtime target size와 비교해 내부 part scaling ratio를 계산하는 기준값이다.

### Runtime Opening 모델 적용 규약 변경

파일:

- `Assets/Scripts/Draw/Wall/Opening/WallOpening.cs`
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Visuals.cs`
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.cs`

변경 내용:

- `WallOpening.ApplyModelPrefab()` 시그니처에 parametric 인자를 추가했다.
  - `useParametricModel`
  - `parametricAuthoredSize`
- `WallOpeningPlacementManager.Visuals.cs`에서 catalog definition의 parametric 정보를 `ApplyModelPrefab()`에 전달한다.
- parametric 모델 판별 기준:
  - catalog의 `useParametricModel == true`
  - 또는 prefab 내부에 `ParametricOpeningModel`이 존재
  - 또는 child 이름에 `Fixed_` / `Stretch_` part가 존재
- parametric 모델이면 `modelScaleRoot.localScale`은 `modelScaleMultiplier`만 반영하고, opening width/height/depth에 따른 전체 fit scale을 적용하지 않는다.
- parametric 모델이면 `ParametricOpeningModel.ApplyOpeningSize(modelTargetSize, effectiveReferenceSize)`를 호출한다.

적용 규약:

- 전체 prefab scale은 opening 크기 보정에 사용하지 않는다.
- 고정 파트는 원래 mesh 크기와 scale을 유지한다.
- stretch 파트만 이름 규약에 따라 필요한 축으로 늘어난다.
- 좌우 프레임, 난간 post, 세로 bar 등은 크기를 유지하고 위치만 opening 폭에 따라 재배치한다.
- glass, rail 등은 지정된 축으로만 늘어난다.

### B001 FBX/Prefab Import 규약 정리

파일:

- `Assets/Prefabs/Furniture/Models/Prefabs/Window/Balcony_Parametric.fbx`
- `Assets/Prefabs/Furniture/Models/Prefabs/Window/Balcony_Parametric.fbx.meta`
- `Assets/Prefabs/Furniture/Models/Prefabs/Window/B001_1.prefab`

변경 내용:

- `Balcony_Parametric.fbx.meta`의 `bakeAxisConversion`을 `1`로 변경했다.
- `B001_1.prefab`의 root rotation override를 identity로 정리했다.
  - `m_LocalRotation.w: 1`
  - `m_LocalRotation.x: 0`
- `OpeningTypeCatalog.asset`의 B001 `modelLocalEulerAngles`를 `{x: 0, y: 0, z: 0}`으로 정리했다.

적용 규약:

- Blender/FBX 축 보정 회전이 prefab variant root에 남아 있으면 런타임에서 opening 회전과 중첩되어 난간이 뒤로 숨거나, 깊이/높이 축이 잘못 해석될 수 있다.
- 따라서 B001 prefab은 Unity prefab 단계에서 identity rotation을 갖는 것을 기준으로 한다.
- WallOpening 쪽의 기존 `ModelToOpeningRotation`은 Unity opening 배치 규약을 위한 공통 변환으로 유지한다.

### JSON Export 규약 확장

파일:

- `Assets/Scripts/Export/LhSceneSchema.cs`
- `Assets/Scripts/Export/LhSceneExportBuilder.Walls.cs`

변경 내용:

- `LhDoorDto`, `LhWindowDto`에 parametric 필드를 추가했다.
- `BuildDoor()`, `BuildWindow()`에서 opening의 실제 치수를 JSON에 기록하도록 변경했다.
- `Resources.Load<OpeningTypeCatalog>("OpeningTypeCatalog")`로 catalog를 조회해 해당 opening의 parametric profile과 authored size를 export한다.

적용 규약:

- `window.code`는 계속 `B001`로 export한다.
- `window.scale`은 기존 Viewer 호환 필드로 유지한다.
- 새 Viewer는 `window.width`, `window.height`, `window.depth`, `window.bottomY`를 우선 사용해 parametric part를 재구성한다.
- `parametricProfileKey`가 빈 문자열이면 기존 static prefab window로 취급할 수 있다.

## LHM_260212 Viewer 변경사항

### B001 Asset 추가

파일:

- `Assets/Test/Models/Prefabs/Window/Balcony/B001_1.prefab`
- `Assets/Test/Models/Prefabs/Window/Balcony/B001_1.prefab.meta`
- `Assets/Test/Models/Prefabs/Window/Balcony/Balcony_Parametric.fbx`
- `Assets/Test/Models/Prefabs/Window/Balcony/Balcony_Parametric.fbx.meta`
- `Assets/Scripts/Room/Opening/ParametricOpeningModel.cs`
- `Assets/Scripts/Room/Opening/ParametricOpeningModel.cs.meta`

변경 내용:

- Editor 프로젝트에서 생성/정리한 B001 prefab과 FBX를 Viewer 프로젝트로 복사했다.
- Editor와 같은 `ParametricOpeningModel` 스크립트를 Viewer에도 추가했다.
- Viewer의 B001 FBX meta도 `bakeAxisConversion: 1` 상태로 동기화했다.

적용 규약:

- Editor와 Viewer가 같은 prefab 구조와 같은 part naming 규약을 사용한다.
- Viewer는 JSON만으로도 `B001`을 같은 profile로 해석할 수 있어야 한다.
- Viewer에서 별도 전체 scale 보정 없이 `ParametricOpeningModel`이 내부 part를 리사이즈한다.

### Window Prefab Mapping 추가

파일:

- `Assets/Test/Models/Prefabs/Window.asset`

변경 내용:

- Viewer prefab preset에 `B001` mapping을 추가했다.
- `window.code: "B001"`가 들어온 JSON을 import할 때 `PrefabLibrary.Instance.GetWindow("B001")`가 실패하지 않도록 했다.

적용 규약:

- JSON의 `window.code`와 Viewer `PrefabPreset.prefabName`은 정확히 일치해야 한다.
- Editor export는 `B001`을 그대로 내보낸다.
- Viewer preset도 `B001` 키를 사용한다.

### JSON DTO 확장

파일:

- `Assets/Scripts/App/Manager/RoomSerializer.cs`

변경 내용:

- `WindowDatabase`에 parametric 필드를 추가했다.
- 추가 필드:
  - `parametricProfileKey`
  - `authoredSize`
  - `width`
  - `height`
  - `depth`
  - `bottomY`

적용 규약:

- `JsonUtility.FromJson<RoomData>()`는 선언된 필드만 역직렬화하므로 Viewer에도 동일 필드가 있어야 한다.
- 기존 JSON에는 이 필드가 없어도 기본값으로 처리된다.
- 새 JSON에서 `parametricProfileKey` 또는 `code == "B001"`이 확인되면 parametric window 경로로 처리한다.

### Viewer Window 생성 규약 변경

파일:

- `Assets/Scripts/Room/Data/WallTest.cs`

변경 내용:

- `CreateWindow()`에서 parametric window 여부를 판별한다.
- 판별 기준:
  - `window.parametricProfileKey`가 비어 있지 않음
  - 또는 `window.code == "B001"`
- parametric window인 경우:
  - wrapper `windowObject`의 local scale을 `parent.lossyScale`의 역수로 설정한다.
  - prefab 전체가 wall segment scale을 다시 상속받아 늘어나는 것을 막는다.
  - instantiate한 prefab 내부의 `ParametricOpeningModel`을 찾아 `ApplyOpeningSize()`를 호출한다.
- `ApplyOpeningSize()` target size는 우선 JSON의 `width/height/depth`를 사용한다.
- `authoredSize`가 JSON에 없거나 0이면 fallback으로 `(1.8, 2.1, 0.14)`를 사용한다.

적용 규약:

- 기존 일반 window는 기존처럼 `segment.window.scale`을 wrapper scale로 사용한다.
- `B001`은 `scale`만으로 전체 prefab을 늘리지 않는다.
- `B001`은 JSON의 실제 opening 치수로 내부 part를 재계산한다.
- 세로 bar, post, frame thickness는 고정 크기를 유지해야 한다.
- rail, glass 등 stretch part만 필요한 축으로 늘어난다.

## 검증 기록

### Editor 프로젝트

명령:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -logFile 'E:\Unity\LH Editor_Refactoring\Temp\parametric-editor-compile-final.log' -quit
```

결과:

- `Tundra build success`
- `AssetDatabase: script compilation time` 확인
- C# compile error 없음

비고:

- `-runTests`도 시도했으나 `Temp\parametric-opening-tests.xml` 결과 파일이 생성되지 않았다.
- 따라서 자동 테스트 결과는 확보하지 못했고, batchmode compile 검증만 완료했다.

### Viewer 프로젝트

명령:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LHM_260212' -logFile 'E:\Unity\LHM_260212\Temp\parametric-viewer-compile-3.log' -quit
```

결과:

- `Tundra build success`
- `AssetDatabase: script compilation time` 확인
- `Exiting batchmode successfully now`
- C# compile error 없음

비고:

- 첫 Viewer batchmode 실행 중 Unity가 `Packages/manifest.json` 패키지 버전을 자동 갱신했으나, B001 작업과 무관하므로 되돌렸다.
- 이후 재검증에서 compile 성공을 확인했다.

## 확인해야 할 수동 QA 항목

Unity Editor를 다시 열면 B001 FBX가 `bakeAxisConversion: 1` 기준으로 reimport된다. 이후 아래 항목을 확인한다.

1. `B001` opening을 생성한다.
2. bottomY를 `0.05`, height를 `2.1`, width를 `1.8`로 설정한다.
3. 원본 prefab preview와 런타임 instance의 방향이 일치하는지 확인한다.
4. 난간이 창호 앞쪽에 보이는지 확인한다.
5. width를 더 크게 변경했을 때:
   - 좌우 frame thickness가 유지되는지 확인한다.
   - 세로 난간 bar가 찌그러지지 않는지 확인한다.
   - rail은 X 방향으로만 늘어나는지 확인한다.
6. height를 변경했을 때:
   - 상단/하단 frame thickness가 유지되는지 확인한다.
   - glass panel이 세로 방향으로만 자연스럽게 늘어나는지 확인한다.
   - 난간 높이와 bar 두께가 비정상적으로 커지지 않는지 확인한다.
7. JSON export 후 Viewer에서 import한다.
8. Viewer에서도 B001이 같은 위치, 같은 크기, 같은 방향으로 보이는지 확인한다.

## 남은 리스크

- 현재 B001 prefab은 FBX 기반 prefab variant이므로 Unity reimport 후 실제 hierarchy/fileID가 바뀌면 prefab reference를 다시 확인해야 한다.
- `ParametricOpeningModel`은 이름 규칙 기반이므로 Blender에서 part 이름이 바뀌면 리사이즈 규칙이 깨질 수 있다.
- `B001` 외 다른 parametric door/window를 추가할 경우 `parametricProfileKey`별 세부 규칙을 분리하는 구조가 필요할 수 있다.
- Test Runner 결과 XML이 생성되지 않은 원인은 별도 확인이 필요하다. 현재는 compile 검증만 완료된 상태다.
