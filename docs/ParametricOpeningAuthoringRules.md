# Parametric Opening Authoring Rules

- Updated: 2026-07-16 16:45:17 +09:00
- Target projects:
  - Editor: `E:\Unity\LH Editor_Refactoring`
  - Viewer: `E:\Unity\LHM_260212`

이 문서는 문, 현관문, 창문, 베란다 창호 모델을 추가할 때 지켜야 하는 최신 규약이다.
핵심 목표는 opening의 `width`, `height`, `depth`, `bottomY`가 달라져도 프레임, 손잡이, 도어락, 힌지, 난간, 레일, 유리 두께가 찌그러지거나 비정상적으로 늘어나지 않게 하는 것이다.

## 1. 공통 원칙

Parametric opening 모델은 prefab 전체를 non-uniform scale로 늘리지 않는다.

대신 모델을 다음 파트로 분리한다.

- `Fixed_`: 크기는 고정하고 위치만 opening edge 기준으로 재배치한다.
- `Stretch_`: 지정된 축만 늘어난다. 두께나 깊이는 고정한다.
- `Repeat_`: 크기는 고정하고 개수 또는 간격만 재계산한다.
- `Optional_`: profile 조건에 따라 켜고 끈다.

Unity `OpeningTypeCatalog`의 parametric 모델은 기본적으로 다음 값을 사용한다.

```text
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
modelLocalPosition = (0, 0, 0)
modelLocalEulerAngles = (0, 0, 0)
modelScaleMultiplier = (1, 1, 1)
```

## 2. 단위 규약

현재 Editor 프로젝트의 opening 치수 단위는 다음을 따른다.

```text
1 Unity unit = 100mm
900mm  = 9.0
950mm  = 9.5
1100mm = 11.0
1800mm = 18.0
2100mm = 21.0
```

따라서 catalog, prefab authored size, runtime resize 코드, JSON export/import는 모두 같은 단위를 사용해야 한다.

## 3. 축 규약

Unity 최종 prefab 기준 축은 다음이 원칙이다.

```text
Unity local X = opening width
Unity local Y = opening height
Unity local Z = opening depth
```

신규 모델은 가능한 이 축 규약으로 맞춘다.

단, Blender에서 만든 기존/일부 모델은 다음 축으로 생성되어 있다.

```text
Blender local X = width
Blender local Y = depth
Blender local Z = height
```

이 모델은 `OpeningTypeCatalog.usesBlenderLocalAxes = true`로 등록한다.
해당 플래그가 true이면 Unity 런타임에서 opening size를 모델 local size로 변환해서 적용한다.

중요:

- 같은 모델에 FBX import 회전, prefab root 회전, catalog 회전을 중복 적용하지 않는다.
- root rotation은 가능한 identity를 유지한다.
- root scale과 child scale은 가능한 `(1, 1, 1)`로 둔다.
- 기존 모델을 억지로 보정하기 위해 `modelLocalEulerAngles`를 임시로 바꾸는 방식은 장기 규약이 아니다.

## 4. Door 규약

문은 창문과 별도 profile로 관리한다.
현관문도 door 규약에 포함된다.

### 4.1 Single Entrance Door

단일 현관문 profile이다.

```text
profileKey = SINGLE_ENTRANCE_DOOR_V1
권장 폭 = 900mm ~ 1100mm
권장 높이 = 2000mm ~ 2200mm
권장 깊이 = 50mm ~ 150mm
```

현재 E003은 이 profile에 해당한다.

E003 catalog 권장값:

```text
openingType = Door
typeKey = E003
displayName = 현관문
referenceSize = (9.5, 21, 1)
authoredSize = (9.5, 21, 1)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
parametricProfileKey = SINGLE_ENTRANCE_DOOR_V1
usesBlenderLocalAxes = true
minSize = (9, 20, 0.5)
maxSize = (11, 22, 1.5)
```

E003 모델링 규칙:

- 좌/우 프레임은 `Fixed_Frame_Left`, `Fixed_Frame_Right`로 둔다.
- 좌/우 프레임은 폭이 바뀌어도 두께를 유지하고 opening 좌우 edge에 붙는다.
- 상/하 프레임은 `Stretch_Frame_Top`, `Stretch_Frame_Bottom`으로 둔다.
- 도어 leaf는 `Stretch_DoorLeaf_Center`로 두되, E003 범위 안에서만 폭/높이 변화가 허용된다.
- 도어락, 손잡이, 힌지, 클로저는 모두 `Fixed_`로 둔다.
- 도어락과 손잡이는 opening 중심이 아니라 실제 문짝의 latch side 기준 inset으로 배치한다.
- 힌지는 실제 문짝의 hinge side 기준 inset으로 배치한다.

금지:

- E003을 1200mm 이상으로 계속 늘리는 것.
- 단일 문짝을 1800mm까지 stretch하는 것.
- 도어락/손잡이/힌지를 opening 폭 비율로 이동시키는 것.

### 4.2 Entrance Door With Side Panel

현관문 옆에 고정 또는 가변 side panel이 있는 profile이다.
넓은 현관문은 단일 문짝을 늘리지 않고 이 profile로 분리한다.

```text
profileKey = ENTRANCE_DOOR_SIDE_PANEL_V1
권장 폭 = 1200mm ~ 1900mm
권장 높이 = 2000mm ~ 2200mm
권장 깊이 = 50mm ~ 150mm
```

권장 catalog 값:

```text
openingType = Door
typeKey = E004
displayName = 현관문(측면패널)
referenceSize = (18, 21, 1)
authoredSize = (18, 21, 1)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
parametricProfileKey = ENTRANCE_DOOR_SIDE_PANEL_V1
usesBlenderLocalAxes = true
minSize = (12, 20, 0.5)
maxSize = (19, 22, 1.5)
```

모델링 규칙:

- 실제 문짝 폭은 900mm 내외로 유지한다.
- 추가 폭은 `Stretch_SidePanel_*` 또는 별도 fixed side panel이 흡수한다.
- 손잡이, 도어락, 힌지는 side panel이 아니라 실제 문짝 기준으로 붙는다.
- 가운데 divider, meeting stile, edge trim은 `Fixed_`로 두고 위치만 재배치한다.
- 상/하 프레임과 threshold만 전체 opening 폭에 맞춰 `Stretch_`한다.

### 4.3 Double Door

양개문은 별도 profile로 만든다.

```text
예정 profileKey = DOUBLE_ENTRANCE_DOOR_V1
권장 폭 = 1200mm ~ 2200mm
```

규칙:

- 좌/우 leaf를 각각 별도 파트로 둔다.
- 중앙 meeting stile은 고정 두께로 유지한다.
- 손잡이와 도어락은 active leaf 기준으로 배치한다.
- 단일 leaf를 과도하게 stretch하지 않는다.

### 4.4 White Interior Door Profiles

화이트 톤 실내 문 3종은 모두 `Door` 카테고리로 등록한다.
창호처럼 보이는 모델도 사용자가 문 카테고리에서 선택해야 한다면 `openingType = Door`를 유지한다.

#### D101: White One Sliding Door

```text
openingType = Door
typeKey = D101
displayName = 화이트 원슬라이딩 도어
parametricProfileKey = WHITE_ONE_SLIDING_DOOR_V1
referenceSize = (9, 21, 0.8)
authoredSize = (9, 21, 0.8)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
usesBlenderLocalAxes = true
minSize = (7, 20, 0.5)
maxSize = (12, 24, 1.2)
```

Blender script:

```text
tools/Blender/create_d101_white_one_sliding_door.py
```

규칙:

- 반투명 주 패널은 `Stretch_DoorPanel_TranslucentGlass`로 둔다.
- 좌우/상하 프레임은 흰색 고정 두께를 유지한다.
- 긴 세로 손잡이는 `Fixed_Handle_`로 두고 scale하지 않는다.
- 레일/가이드 두께는 고정하고 X 방향만 늘린다.

#### D102: White Swing Door

```text
openingType = Door
typeKey = D102
displayName = 화이트 여닫이문
parametricProfileKey = WHITE_SWING_DOOR_V1
referenceSize = (9, 21, 0.8)
authoredSize = (9, 21, 0.8)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
usesBlenderLocalAxes = true
minSize = (7, 20, 0.5)
maxSize = (11, 23, 1.2)
```

Blender script:

```text
tools/Blender/create_d102_white_swing_door.py
```

규칙:

- 흰색 도어판은 `Stretch_DoorPanel_MainPanel`로 둔다.
- 손잡이/노브/힌지는 `Fixed_`로 둔다.
- 손잡이가 우측 latch side에 있어야 하면 이름에 `_Right_`를 포함해 right edge anchor를 탄다.

#### D103: White Glass Frame Door

```text
openingType = Door
typeKey = D103
displayName = 화이트 창호형 도어
parametricProfileKey = WHITE_GLASS_FRAME_DOOR_V1
referenceSize = (18, 21, 1)
authoredSize = (18, 21, 1)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
usesBlenderLocalAxes = true
minSize = (12, 18, 0.5)
maxSize = (24, 24, 1.5)
```

Blender script:

```text
tools/Blender/create_d103_white_glass_frame_door.py
```

규칙:

- 문 카테고리지만 창호형이므로 유리 패널과 mullion을 별도 part로 둔다.
- `Stretch_DoorPanel_LeftGlass`, `Stretch_DoorPanel_RightGlass`만 폭/높이 변화에 반응한다.
- 중앙 mullion은 `Fixed_VerticalTrim_CenterMullion`으로 두고 두께는 고정한다.

## 5. Window 규약

창문은 door와 별도 profile로 관리한다.

기본 규칙:

- 외곽 프레임 두께는 고정한다.
- mullion, sash, rail 두께는 고정한다.
- 유리 두께는 고정한다.
- glass pane의 남는 면적만 X/Y 방향으로 stretch한다.
- 손잡이/잠금장치는 `Fixed_`로 둔다.

권장 이름:

```text
Fixed_OuterFrame_Left
Fixed_OuterFrame_Right
Stretch_OuterFrame_Top
Stretch_OuterFrame_Bottom
Fixed_Center_Mullion
Stretch_Glass_Pane_01
Stretch_Glass_Pane_02
Fixed_Window_Lock
```

## 6. Balcony Railing Window 규약

B001은 window 타입에 속하며, 베란다 난간이 포함된 창호 profile이다.

```text
profileKey = BALCONY_RAILING_WINDOW_V1
authoredSize = (18, 21, 1.4)
권장 폭 = 1200mm ~ 2400mm
권장 높이 = 1800mm ~ 2400mm
```

B001 catalog 권장값:

```text
openingType = Window
typeKey = B001
displayName = 베란다(난간형)
referenceSize = (18, 21, 1.4)
authoredSize = (18, 21, 1.4)
fitWidth = false
fitHeight = false
fitDepth = false
useParametricModel = true
parametricProfileKey = BALCONY_RAILING_WINDOW_V1
usesBlenderLocalAxes = false
minSize = (12, 18, 0.5)
maxSize = (24, 24, 2)
```

B001 모델링 규칙:

- 창호 frame은 opening 상/하/좌/우에 여백 없이 맞아야 한다.
- 유리 패널은 opening 폭/높이에 따라 stretch한다.
- 난간 높이는 opening height 변화에 따라 변하면 안 된다.
- 난간은 opening 하단 기준으로 anchor된다.
- 난간 top/mid/bottom rail은 X 방향만 stretch한다.
- 난간 vertical bar는 scale하지 않는다.
- vertical bar는 authored spacing을 기준으로 개수 또는 간격을 재분배한다.
- Viewer와 Editor에서 실내/실외 방향이 다르게 보이면 prefab 자체를 뒤집지 말고 placement/import 쪽 방향 기준을 먼저 점검한다.

권장 이름:

```text
Fixed_BalconyFrame_Left
Fixed_BalconyFrame_Right
Stretch_BalconyFrame_Top
Stretch_BalconyFrame_BottomRail
Fixed_Center_Mullion
Stretch_Glass_Left_SlidingPanel
Stretch_Glass_Right_SlidingPanel
Fixed_Railing_Left_Post
Fixed_Railing_Right_Post
Stretch_Railing_Top_Rail
Stretch_Railing_Mid_Rail
Stretch_Railing_Bottom_Rail
Fixed_Railing_Vertical_Bar_01
Fixed_Railing_Vertical_Bar_02
```

## 7. Blender Python 작성 규약

Blender Python으로 생성할 때는 다음을 지킨다.

- cube primitive 또는 단순 mesh helper로 각 part를 별도 object로 만든다.
- `dimensions` 설정 후 scale apply를 수행한다.
- 최종 child object scale은 가능한 `(1, 1, 1)`이 되게 한다.
- root empty에는 custom property를 기록한다.
- material은 의미 단위로만 나눈다. 과도한 texture/material slot을 만들지 않는다.

권장 root custom properties:

```python
root["lh_opening_type"] = "Door"  # or "Window"
root["lh_target_width"] = target_width
root["lh_target_height"] = target_height
root["lh_target_depth"] = target_depth
root["lh_modeling_rule"] = "fixed_parts_plus_stretch_parts"
root["lh_parametric_profile_key"] = "SINGLE_ENTRANCE_DOOR_V1"
```

권장 helper:

```python
def create_box(name, location, size, material=None, parent=None, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material:
        obj.data.materials.append(material)
    if parent:
        obj.parent = parent
    return obj
```

## 8. Material 규약

Editor와 Viewer 양쪽에서 깨지지 않게 단순 material을 우선한다.

권장 material:

```text
EntranceDoor_DarkTrim
EntranceDoor_DarkMainPanel
EntranceDoor_LightSidePanel
EntranceDoor_BrushedMetal
Balcony_Frame
Balcony_Glass
Balcony_Rail
Window_Frame
Window_Glass
```

규칙:

- Viewer에 없는 shader를 직접 참조하지 않는다.
- 가능하면 URP/Lit 또는 프로젝트 내 단순 custom shader만 사용한다.
- 투명 유리는 Viewer에도 같은 shader와 material이 복사되어야 한다.
- texture를 쓸 경우 prefab dependency에 포함시켜 Editor와 Viewer를 같이 동기화한다.

## 9. JSON Export/Import 규약

Editor에서 내보내는 JSON은 기존 viewer v1 필드와 함께 parametric 필드를 포함할 수 있다.

opening별 권장 필드:

```json
{
  "isExist": true,
  "code": "B001",
  "parametricProfileKey": "BALCONY_RAILING_WINDOW_V1",
  "authoredSize": { "x": 18, "y": 21, "z": 1.4 },
  "width": 18,
  "height": 21,
  "depth": 1.4,
  "bottomY": 0.5
}
```

Viewer는 `code`만으로도 prefab을 찾을 수 있어야 하고, `parametricProfileKey`가 있으면 전체 prefab scale 대신 내부 part resize 경로를 사용해야 한다.

## 10. Editor-Viewer 동기화 규약

Editor에서 모델을 추가하거나 수정할 때 Viewer에서도 바로 사용하려면 별도 동기화 도구가 필요하다.
수동 복사는 다음 문제를 반복해서 만든다.

- prefab은 복사됐지만 material/shader/texture가 누락된다.
- catalog 값은 바뀌었지만 Viewer mapping은 갱신되지 않는다.
- Editor와 Viewer의 `ParametricOpeningModel` 코드가 달라진다.
- JSON에는 새 profile이 들어가지만 Viewer가 해석하지 못한다.
- 실내/실외 방향 보정을 Editor와 Viewer에 다르게 적용한다.

권장 도구:

```text
Unity Editor menu:
LH > Opening Models > Sync Selected Opening Model To Viewer
LH > Opening Models > Sync All Parametric Opening Models To Viewer
```

도구가 해야 할 일:

1. `OpeningTypeCatalog.asset`에서 선택한 `typeKey`를 읽는다.
2. `modelPrefab`과 `AssetDatabase.GetDependencies(prefabPath, true)` 결과를 수집한다.
3. FBX, prefab, mesh asset, material, shader, texture, thumbnail을 Viewer 프로젝트로 복사한다.
4. Viewer의 prefab lookup database 또는 preset에 같은 `code`를 등록한다.
5. Viewer의 catalog/manifest에 다음 metadata를 기록한다.

```text
typeKey
openingType
parametricProfileKey
authoredSize
referenceSize
minSize
maxSize
usesBlenderLocalAxes
fitWidth/fitHeight/fitDepth
```

6. Viewer에 필요한 runtime script 버전을 검사한다.
7. 동기화 후 Viewer에서 누락 dependency가 있는지 검증한다.

단기적으로는 EditorWindow 기반 복사 도구가 가장 빠르다.
장기적으로는 `LH.OpeningModels` 같은 shared Unity package를 만들어 Editor와 Viewer가 같은 asset/script/catalog package를 참조하는 방식이 더 안정적이다.

## 11. B001 Unity Axis Prefab Generator의 역할

`Assets/Editor/B001UnityAxisPrefabGenerator.cs`는 B001을 FBX가 아니라 Unity Editor 코드로 직접 생성하는 임시/보조 생성기다.

역할:

- Unity 축 기준 prefab을 생성한다.
- root rotation/scale을 identity로 유지한다.
- `B001_1.prefab`과 `B001_1_Meshes.asset`을 생성한다.
- B001용 단순 material을 보장한다.
- `ParametricOpeningModel`을 붙이고 authored size를 기록한다.

즉 “B001 Unity Exis Prefab”은 runtime에서 특별히 동작하는 기능이 아니라,
B001 prefab을 Unity 축 기준으로 다시 만들기 위한 Editor 생성 도구로 보는 것이 맞다.

주의:

- 이 생성기는 B001 전용이다.
- 모든 문/창문을 이 방식으로 만들 수는 없다.
- 장기적으로는 Blender Python 생성물 또는 공통 authoring tool이 source of truth가 되어야 한다.
- Viewer까지 자동 반영하려면 이 생성기만으로 부족하고 sync 도구가 별도로 필요하다.

## 12. 신규 모델 추가 체크리스트

새 문/창문 모델을 추가할 때는 아래 순서로 진행한다.

1. Door인지 Window인지 먼저 결정한다.
2. profile 범위를 정한다. 하나의 profile이 너무 넓은 폭 범위를 담당하지 않게 한다.
3. authored size, min size, max size를 정한다.
4. Blender Python에서 `Fixed_`, `Stretch_`, `Repeat_`, `Optional_` 파트를 분리한다.
5. root custom property에 profile key와 target size를 기록한다.
6. Unity에 import한 뒤 root rotation/scale이 규약에 맞는지 확인한다.
7. prefab에 `ParametricOpeningModel`을 붙인다.
8. `OpeningTypeCatalog`에 fit false, parametric true로 등록한다.
9. min/max 범위에서 resize 테스트를 한다.
10. JSON export/import 테스트를 한다.
11. Viewer 프로젝트에 sync 도구로 prefab과 dependency를 반영한다.
12. Viewer에서 같은 JSON을 열어 방향, 재질, 여백, frame fill, 내부 part resize를 확인한다.
