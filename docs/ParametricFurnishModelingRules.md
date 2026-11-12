# Parametric Furnish Modeling Rules

- Updated: 2026-08-07
- Target project: `E:\Unity\LH Editor_Refactoring`
- Runtime catalog: `Assets/Prefabs/Furniture/FurnitureCatalog.asset`
- Runtime scripts:
  - `Assets/Scripts/Furniture/FurnitureCatalog.cs`
  - `Assets/Scripts/Furniture/FurnitureInstance.cs`
  - `Assets/Scripts/Furniture/FurniturePlacementManager.cs`

이 문서는 새 furnish 모델을 만들 때 지켜야 하는 제작 규약이다. 목표는 FBX 파일에 의존하지 않고도 Unity 안에서 재생성 가능한 furnish prefab을 만들고, 배치, 저장/로드, 내보내기 동작을 안정적으로 유지하는 것이다.

## 1. 기본 방향

새 furnish는 가능한 한 Unity primitive 또는 procedural mesh로 만든다.

FBX는 기본 제작 방식이 아니다. 다음 경우에만 예외적으로 허용한다.

- 곡면 위생도기처럼 primitive 조합만으로 품질이 부족한 경우
- 외부 공급 모델을 그대로 검수해야 하는 경우
- 이미 승인된 고정 형상이고 파라미터화할 실익이 없는 경우

FBX를 쓰더라도 최종 runtime 단위는 반드시 Unity prefab과 `FurnitureCatalogItem`이어야 한다. 배치 시스템은 FBX가 아니라 catalog item과 prefab transform, renderer bounds, collider를 기준으로 동작한다.

## 2. 단위 규약

현재 furnish와 opening 계열은 다음 단위계를 따른다.

```text
1 Unity unit = 100 mm
10 Unity units = 1,000 mm
```

예시:

```text
600 mm  = 6.0
850 mm  = 8.5
1,200 mm = 12.0
2,200 mm = 22.0
```

새 모델 치수, collider, catalog `boundsSize`, `placementOffset`, export/import transform은 모두 같은 단위를 사용한다.

## 3. 축과 원점

Unity prefab 기준 축은 다음과 같이 통일한다.

```text
X = width
Y = height
Z = depth
```

원점은 바닥 중앙 footprint를 기본으로 한다.

```text
root position = 바닥 중앙
root rotation = identity
root localScale = (1, 1, 1)
```

벽에 붙는 세면대, 수납장, 샤워기처럼 기준면이 중요한 모델도 root는 바닥 중앙에 둔다. 벽 기준 보정은 `placementOffset` 또는 child transform으로 처리한다.

금지:

- root rotation으로 축 보정하기
- root scale로 실제 치수 맞추기
- FBX import 회전, prefab root 회전, catalog 회전을 중복 적용하기
- 모델 중심이 바닥보다 아래에 있는 prefab 만들기

## 4. Prefab 구조

새 furnish prefab은 다음 구조를 권장한다.

```text
F001
  Visuals
    Fixed_Body_...
    Fixed_Trim_...
    Stretch_Panel_...
  Colliders
    BoundsCollider
```

최소 요구사항:

- prefab 파일명은 catalog `code`와 일치시킨다.
- root GameObject 이름도 catalog `code`와 일치시킨다.
- root에는 선택/겹침 검사용 `BoxCollider`를 둔다.
- 시각 요소는 `Visuals` 아래에 둔다.
- collider helper는 필요하면 `Colliders` 아래에 둔다.
- prefab 자체에 `FurnitureInstance`를 미리 붙이지 않는다. 런타임 생성/로드 코드가 추가한다.

기존 코드가 `GetComponentInParent<FurnitureInstance>()`로 선택 대상을 찾기 때문에 collider는 반드시 prefab root 하위에 있어야 한다.

## 5. Catalog 규약

새 모델은 `Assets/Prefabs/Furniture/FurnitureCatalog.asset`의 `items`에 등록한다.

필드 규약:

```text
code = 내부 catalog 식별자. prefab 이름과 동일 권장.
exportCode = 외부 JSON/계약 코드. 비어 있으면 code가 사용된다.
nativeCode = 외부 native 코드가 있을 때만 입력.
displayName = UI 표시명.
prefab = 생성한 prefab.
thumbnail = UI 썸네일.
placementOffset = pointer hit 지점에서 root를 보정할 값.
defaultEulerAngles = 기본 배치 회전. 가능하면 (0, 0, 0).
boundsSize = renderer bounds 추정값. 검수용 metadata로 유지.
scaleMultiplier = catalog 적용 scale. 새 모델은 1 권장.
defects = furnish 하자 코드 기본값.
```

새로 만드는 모델은 `scaleMultiplier = 1`을 기본으로 한다. 기존 `BT001`, `S001`, `SB001`, `T001`은 legacy 크기 보정 때문에 `0.6~0.8` 값을 쓰고 있다. 새 모델까지 이 방식을 따라가면 실제 치수, collider, 저장/로드 scale이 헷갈리기 쉽다.

## 6. FBX 비의존 제작 방식

FBX 없이 만드는 모델은 세 가지 방식 중 하나를 사용한다.

### 6.1 Primitive Prefab

Unity Cube, Plane, Cylinder, Sphere 등을 조합한다. `CT001`, `CT002`가 이 방식에 가깝다.

적합한 대상:

- 주방 수납장
- 선반
- 장식장
- 직선형 욕실장
- 단순 샤워부스
- 직육면체 기반 설비

장점:

- Git diff가 YAML로 남는다.
- 외부 DCC 툴이 필요 없다.
- 치수 수정이 빠르다.
- material 교체가 쉽다.

### 6.2 Procedural Mesh Generator

Editor script가 prefab을 생성한다. 생성 코드는 `Assets/Editor` 또는 `tools` 아래에 두고, 결과 prefab은 `Assets/Prefabs/Furniture/Models/Prefabs/...`에 저장한다.

적합한 대상:

- 반복 패턴이 있는 선반
- parametric cabinet
- 프레임, 레일, 손잡이처럼 규칙적인 조립 모델
- 여러 치수 variant를 자동 생성해야 하는 모델

규약:

- generator는 idempotent해야 한다. 같은 입력으로 실행하면 같은 prefab 구조가 나온다.
- material asset은 기존 material을 재사용하거나 명시적으로 생성한다.
- prefab root, collider, thumbnail, catalog patch까지 한 번에 처리할 수 있으면 좋다.
- 생성 결과는 Unity prefab으로 저장하고 runtime에서는 generator에 의존하지 않는다.

### 6.3 Mesh Asset 생성

곡면이 약간 필요하지만 FBX까지는 필요 없는 경우, Unity `Mesh`를 코드로 생성해서 `.asset`에 저장한다.

적합한 대상:

- 둥근 손잡이
- 단순 곡면 레일
- 배수구, 캡, 원형 부속
- chamfer가 단순한 부품

주의:

- normal, UV, bounds를 명시적으로 설정한다.
- MeshCollider가 필요하면 단순화된 collider를 따로 둔다.
- render mesh를 충돌 검사용으로 직접 쓰지 않는다.

## 7. Part Naming

파트 이름은 역할이 드러나게 작성한다.

```text
Fixed_ = 크기 고정 부품
Stretch_ = 특정 축으로 늘어날 수 있는 부품
Repeat_ = 개수/간격으로 반복되는 부품
Optional_ = variant 조건에 따라 켜고 끄는 부품
Collider_ = 물리/선택 전용 부품
```

예시:

```text
Fixed_Frame_Left
Fixed_Frame_Right
Stretch_BackPanel
Repeat_Shelf_01
Optional_Handle_Long
Collider_Bounds
```

이름 규약은 당장 runtime이 해석하지 않더라도, generator와 리뷰 기준으로 사용한다.

## 8. Collider 규약

배치 검증은 `FurnitureInstance.CalculateWorldBounds()`와 Physics overlap을 함께 사용한다.

따라서 새 prefab은 다음을 지켜야 한다.

- root 또는 child에 renderer가 있어야 한다.
- root `BoxCollider`는 실제 footprint와 높이를 대략 덮어야 한다.
- collider가 시각 모델보다 과하게 크면 방 밖 판정 또는 겹침 판정이 실패한다.
- collider가 너무 작으면 다른 furnish와 겹쳐도 통과할 수 있다.
- trigger collider는 선택/겹침 기준으로 쓰지 않는다.

권장 collider:

```text
center.x = 0
center.y = height / 2
center.z = 0
size.x = width
size.y = height
size.z = depth
```

벽부착형 모델은 실제 돌출 깊이만 collider에 반영한다. 벽 안쪽으로 들어가는 보정용 geometry는 collider에 포함하지 않는다.

## 9. Placement Offset

`placementOffset`은 pointer hit 지점과 prefab root가 달라야 할 때만 사용한다.

일반 바닥 설치형:

```text
placementOffset = (0, 0, 0)
```

바닥 hit가 모델 바닥보다 낮거나, 기존 모델처럼 root가 시각 중심과 어긋나 있으면 보정값을 둔다.

```text
S001 existing example:
placementOffset = (0, 4, 0)
```

새 모델은 root를 바닥 중앙으로 맞춰서 `placementOffset = (0, 0, 0)`이 되게 만드는 것을 우선한다.

## 10. 하자 코드 규약

`FurnitureCatalogItem.defects`는 기본 하자 코드 후보를 담는다.

```text
mntnCd = 방 코드. 배치/저장/내보내기 때 현재 방 코드로 보정될 수 있다.
locCd = furnish 위치 코드.
mtrlCd = 재료/부위 코드.
```

새 모델 제작 시 하자 코드가 확정되지 않았으면 `defects`는 비워 둔다. 임의 코드로 채우지 않는다.

하자 코드가 필요한 경우:

- 위치 옵션 UI에서 선택할 기본 `locCd`가 있어야 한다.
- 동일 furnish에 여러 재료 하자 후보가 있으면 `mtrlCd`별로 entry를 나눈다.
- `mntnCd`는 room resolve 로직이 덮어쓸 수 있으므로, catalog에는 fallback 값만 둔다.

## 11. 저장/로드 및 Export 영향

저장 시 `LhWorkStateBuilder`는 placed `FurnitureInstance`를 찾아 다음 값을 저장한다.

```text
catalogCode
exportCode
nativeCode
name
furnishLocationCode
position
eulerAngles
localScale
isPlaced
roomName
defects
```

로드 시 `LhWorkStateLoader`는 `catalogCode`, `exportCode`, `nativeCode` 중 하나가 catalog item과 일치해야 prefab을 복원한다.

내보내기 시 `LhSceneExportBuilder`는 room 기준 상대 transform과 `ExportCode`, `NativeCode`, defects를 사용한다.

따라서 새 모델은 다음을 지켜야 한다.

- 한번 배포한 `code`는 함부로 바꾸지 않는다.
- 외부 계약 코드가 필요한 모델은 첫 등록부터 `exportCode`를 확정한다.
- prefab root transform 기준이 바뀌면 기존 저장 파일의 위치가 달라질 수 있다.
- `scaleMultiplier` 변경은 기존 저장 데이터의 체감 크기를 바꿀 수 있다.

## 12. 제작 체크리스트

새 furnish 추가 전:

- 모델 치수를 Unity unit 기준으로 정한다.
- prefab code를 정한다.
- FBX 없이 primitive/procedural로 가능한지 판단한다.
- material과 thumbnail 필요 여부를 정한다.
- exportCode/nativeCode 필요 여부를 확인한다.
- 하자 코드가 확정되어 있는지 확인한다.

Prefab 제작 후:

- root name과 prefab filename이 code와 같은지 확인한다.
- root rotation이 identity인지 확인한다.
- root localScale이 `(1, 1, 1)`인지 확인한다.
- renderer bounds가 실제 크기와 맞는지 확인한다.
- BoxCollider가 실제 footprint와 맞는지 확인한다.
- layer는 런타임에서 `Furnish`로 적용되므로 prefab 고정 layer에 의존하지 않는다.

Catalog 등록 후:

- `FurnitureCatalog.asset`의 `items`에 등록한다.
- `scaleMultiplier = 1`을 우선 사용한다.
- `placementOffset = (0, 0, 0)`이 되는지 검증한다.
- thumbnail이 UI에서 보이는지 확인한다.
- 배치, 회전, 삭제, 저장, 로드, export를 확인한다.

## 13. 권장 폴더

```text
Assets/Prefabs/Furniture/Models/Prefabs/Furnish/{CODE}.prefab
Assets/Prefabs/Furniture/Models/Materials/Furnish/{CODE}/...
Assets/Prefabs/Furniture/Models/Textures/Furnish/{CODE}/...
Assets/Prefabs/Furniture/FurnitureCatalog_Thumbnails/{CODE}.png
```

주방 계열은 기존처럼 `Kitchen` 아래에 둘 수 있다.

```text
Assets/Prefabs/Furniture/Models/Prefabs/Kitchen/{CODE}.prefab
Assets/Prefabs/Furniture/Models/Materials/Kitchen/{CODE}/...
```

## 14. FBX 사용 예외 체크리스트

FBX를 사용해야 한다면 다음 조건을 만족해야 한다.

- FBX import scale은 1이다.
- prefab root rotation은 identity다.
- prefab root scale은 `(1, 1, 1)`이다.
- axis 보정은 import 단계 또는 child transform 중 한 곳에서만 한다.
- collider는 FBX mesh와 별도로 단순 BoxCollider를 둔다.
- catalog `scaleMultiplier`로 단위 보정을 숨기지 않는다.
- 원본 FBX의 라이선스와 출처가 추적 가능하다.

FBX를 쓰더라도 새 제작 기준은 "FBX를 직접 런타임 자산으로 신뢰하지 않고, 검수된 Unity prefab을 runtime contract로 삼는다"이다.

## 15. 결론

FBX에 의존하지 않는 furnish 제작은 가능하다. 특히 수납장, 선반, 샤워부스, 단순 설비는 Unity primitive/procedural 방식이 더 낫다.

다만 변기, 욕조, 세면대처럼 유기적인 곡면 품질이 중요한 모델은 FBX 또는 procedural mesh가 현실적이다. 이 경우에도 runtime 규약은 동일하다. 최종 산출물은 catalog에 등록된 Unity prefab이고, 치수, 원점, collider, export code가 검수 기준이다.
