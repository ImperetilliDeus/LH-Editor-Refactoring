# Blender Python Parametric Opening 모델링 규약

- 작성일시: 2026-07-14 14:25:06 +09:00
- 대상 모델: 문, 창문, 베란다 창호, 난간형 창호 등 opening에 배치되는 모델
- 목적: Unity opening의 width, height, depth가 바뀌어도 프레임, 난간, 손잡이, 힌지, 레일, 유리 두께가 찌그러지거나 비정상적으로 늘어나지 않도록 Blender Python 생성 단계에서 지켜야 할 규약을 정의한다.

## 핵심 원칙

문/창/베란다 모델은 하나의 mesh를 전체 scale하는 방식으로 만들면 안 된다.

대신 다음처럼 분리해서 만든다.

- 고정 파트: 크기는 유지하고 위치만 이동하는 part
- 가변 파트: 특정 축으로만 늘어나는 part
- 장식/기능 파트: 손잡이, 힌지, 잠금장치, 난간 post처럼 크기를 유지하는 part

잘못된 방식:

```python
# 나쁜 예: 완성된 창문 하나를 통째로 X/Z scale
window_root.scale = (target_width / 1.2, 1, target_height / 1.2)
```

이 방식은 frame thickness, 유리 두께, 난간 세로 bar, 손잡이까지 같이 늘어나므로 금지한다.

권장 방식:

```python
# 좋은 예: part별 dimension과 location을 target size로 재계산
create_box("Fixed_Frame_Left", size=(frame_thickness, depth, target_height), ...)
create_box("Fixed_Frame_Right", size=(frame_thickness, depth, target_height), ...)
create_box("Stretch_Glass_Left", size=(glass_width, glass_thickness, glass_height), ...)
```

## 좌표계 규약

Blender Python 생성 단계에서는 다음 기준으로 모델링한다.

- `X`: opening 가로 폭, width
- `Y`: opening 깊이, depth, 벽 두께 방향
- `Z`: opening 세로 높이, height

Unity 최종 prefab에서는 다음 기준으로 해석된다.

- `X`: width
- `Y`: height
- `Z`: depth

따라서 FBX export/import 단계에서 축 보정이 필요하다.

권장:

- Blender 모델링: `X=width`, `Y=depth`, `Z=height`
- Unity FBX import: `bakeAxisConversion = 1`
- Unity prefab root: rotation identity, scale `(1,1,1)`

금지:

- prefab variant root에 `-90도 X rotation` 같은 import 보정 회전을 남기는 것
- catalog에서 다시 `modelLocalEulerAngles.y = 180` 같은 임시 회전으로 맞추는 것
- root scale로 width/height/depth를 맞추는 것

## 원점 규약

모델 원점은 opening 중심에 둔다.

- X 중심: `0`
- Y 중심: `0`
- Z 중심: `0`

즉 target size가 `width=1.8`, `height=2.1`, `depth=0.14`이면 모델의 전체 범위는 대략 다음과 같다.

- X: `-0.9` ~ `0.9`
- Y: `-0.07` ~ `0.07`
- Z: `-1.05` ~ `1.05`

문처럼 바닥에 닿는 모델도 Blender에서 root 자체를 `height / 2`만큼 올리지 않는다.

이유:

- Unity opening placement가 opening 중심 기준으로 배치한다.
- Blender root를 올려 버리면 Unity에서 bottomY 계산과 중복되어 위치가 어긋난다.

## Object Naming 규약

모든 part 이름은 역할과 리사이즈 규칙을 알 수 있게 작성한다.

### Prefix

- `Fixed_`: 크기 고정, 위치만 재배치
- `Stretch_`: 특정 축으로 늘어남

### 권장 이름 예시

문:

- `Fixed_Frame_Left`
- `Fixed_Frame_Right`
- `Fixed_Frame_Top`
- `Fixed_Frame_Bottom`
- `Stretch_Door_Slab_Center`
- `Fixed_Hinge_01`
- `Fixed_Hinge_02`
- `Fixed_Hinge_03`
- `Fixed_Handle_Set`
- `Fixed_Corner_Detail_TopLeft`

창:

- `Fixed_OuterFrame_Left`
- `Fixed_OuterFrame_Right`
- `Fixed_OuterFrame_Top`
- `Fixed_OuterFrame_Bottom`
- `Fixed_Center_Mullion`
- `Stretch_Glass_Pane_01`
- `Stretch_Glass_Pane_02`
- `Stretch_Rail_Top`
- `Stretch_Rail_Bottom`
- `Fixed_Lock_Handle`

베란다:

- `Fixed_BalconyFrame_Left`
- `Fixed_BalconyFrame_Right`
- `Fixed_BalconyFrame_Top`
- `Fixed_BalconyFrame_BottomRail`
- `Fixed_Center_Mullion`
- `Stretch_Glass_Left_SlidingPanel`
- `Stretch_Glass_Right_SlidingPanel`
- `Fixed_Railing_Left_Post`
- `Fixed_Railing_Right_Post`
- `Stretch_Railing_Top_Rail`
- `Stretch_Railing_Mid_Rail`
- `Stretch_Railing_Bottom_Rail`
- `Fixed_Railing_Vertical_Bar_01`
- `Fixed_Railing_Vertical_Bar_02`

## Fixed Part 규약

Fixed part는 target width/height가 바뀌어도 실제 두께와 크기를 유지한다.

예시: 좌우 프레임

```python
frame_thickness = 0.06
frame_depth = 0.08

left_x = -target_width * 0.5 + frame_thickness * 0.5
right_x = target_width * 0.5 - frame_thickness * 0.5

create_box(
    name="Fixed_OuterFrame_Left",
    location=(left_x, 0, 0),
    size=(frame_thickness, frame_depth, target_height),
)

create_box(
    name="Fixed_OuterFrame_Right",
    location=(right_x, 0, 0),
    size=(frame_thickness, frame_depth, target_height),
)
```

여기서 중요한 점:

- `frame_thickness`는 고정값이다.
- width가 커져도 `frame_thickness`는 커지지 않는다.
- width가 커질 때 바뀌는 것은 `left_x`, `right_x` 위치뿐이다.

## Stretch Part 규약

Stretch part는 전체 모델을 scale하지 않고, 실제로 늘어나야 하는 축의 mesh dimension만 크게 만든다.

예시: 유리 pane

```python
inner_width = target_width - frame_thickness * 2 - mullion_thickness
pane_width = inner_width * 0.5
glass_height = target_height - frame_thickness * 2

create_box(
    name="Stretch_Glass_Left_SlidingPanel",
    location=(-pane_width * 0.5, 0, 0),
    size=(pane_width, glass_thickness, glass_height),
)
```

여기서 중요한 점:

- `glass_thickness`는 고정값이다.
- width가 바뀌면 `pane_width`만 바뀐다.
- height가 바뀌면 `glass_height`만 바뀐다.
- depth 방향 두께는 늘리지 않는다.

## 난간 모델링 규약

난간은 특히 찌그러짐이 눈에 잘 띄므로 고정 파트와 가변 파트를 엄격히 나눈다.

### 난간 post

좌우 post는 크기 고정, 위치만 이동한다.

```python
post_size = 0.045
railing_height = 0.75
railing_z = -target_height * 0.5 + railing_height * 0.5 + 0.12

create_box(
    name="Fixed_Railing_Left_Post",
    location=(-target_width * 0.5 + post_size * 0.5, -0.09, railing_z),
    size=(post_size, post_size, railing_height),
)

create_box(
    name="Fixed_Railing_Right_Post",
    location=(target_width * 0.5 - post_size * 0.5, -0.09, railing_z),
    size=(post_size, post_size, railing_height),
)
```

### 난간 horizontal rail

가로 rail은 X 방향만 늘어난다.

```python
rail_thickness = 0.035
rail_width = target_width

create_box(
    name="Stretch_Railing_Top_Rail",
    location=(0, -0.09, railing_z + railing_height * 0.5),
    size=(rail_width, rail_thickness, rail_thickness),
)
```

여기서 `rail_thickness`는 width가 바뀌어도 그대로 유지한다.

### 난간 vertical bar

세로 bar는 크기 고정, 개수와 위치를 width에 맞춰 재분배한다.

```python
bar_width = 0.018
bar_depth = 0.018
bar_height = 0.62
max_gap = 0.14

available_width = target_width - post_size * 2
bar_count = max(2, int(available_width / max_gap))

for i in range(bar_count):
    t = (i + 1) / (bar_count + 1)
    x = -available_width * 0.5 + available_width * t
    create_box(
        name=f"Fixed_Railing_Vertical_Bar_{i + 1:02d}",
        location=(x, -0.09, railing_z),
        size=(bar_width, bar_depth, bar_height),
    )
```

중요:

- bar의 `bar_width`, `bar_depth`, `bar_height`는 고정이다.
- width가 커지면 bar를 scale하지 않고 간격 또는 개수를 조정한다.
- `Fixed_Railing_Vertical_Bar_01` 같은 명확한 이름을 유지한다.

## 문 모델링 예시

문은 frame, slab, hinge, handle을 분리한다.

```python
def create_parametric_door(
    target_width=0.9,
    target_height=2.1,
    target_depth=0.05,
    frame_thickness=0.07,
    frame_depth=0.08,
    slab_thickness=0.035,
):
    min_width = frame_thickness * 2 + 0.35
    min_height = frame_thickness * 2 + 0.7
    if target_width < min_width or target_height < min_height:
        raise ValueError("Door target size is too small for fixed parts.")

    create_box(
        "Fixed_Frame_Left",
        (-target_width * 0.5 + frame_thickness * 0.5, 0, 0),
        (frame_thickness, frame_depth, target_height),
    )
    create_box(
        "Fixed_Frame_Right",
        (target_width * 0.5 - frame_thickness * 0.5, 0, 0),
        (frame_thickness, frame_depth, target_height),
    )
    create_box(
        "Fixed_Frame_Top",
        (0, 0, target_height * 0.5 - frame_thickness * 0.5),
        (target_width, frame_depth, frame_thickness),
    )

    slab_width = target_width - frame_thickness * 2
    slab_height = target_height - frame_thickness
    create_box(
        "Stretch_Door_Slab_Center",
        (0, 0, -frame_thickness * 0.5),
        (slab_width, slab_thickness, slab_height),
    )

    handle_x = target_width * 0.5 - frame_thickness - 0.08
    handle_z = -target_height * 0.5 + target_height * 0.48
    create_handle("Fixed_Handle_Set", (handle_x, -slab_thickness * 0.6, handle_z))
```

문 손잡이는 반드시 `Fixed_`로 둔다.

금지:

- 문 폭이 커졌다고 손잡이 길이를 늘리는 것
- 문 높이가 커졌다고 힌지 두께를 늘리는 것
- slab과 frame을 하나의 cube로 만드는 것

## 창 모델링 예시

창은 outer frame, mullion, sash, glass를 분리한다.

```python
def create_parametric_window(
    target_width=1.2,
    target_height=1.2,
    frame_thickness=0.06,
    mullion_thickness=0.045,
    glass_thickness=0.008,
):
    min_glass = 0.25
    min_width = frame_thickness * 2 + mullion_thickness + min_glass * 2
    min_height = frame_thickness * 2 + min_glass
    if target_width < min_width or target_height < min_height:
        raise ValueError("Window target size is too small for fixed frame and glass.")

    create_box(
        "Fixed_OuterFrame_Left",
        (-target_width * 0.5 + frame_thickness * 0.5, 0, 0),
        (frame_thickness, frame_thickness, target_height),
    )
    create_box(
        "Fixed_OuterFrame_Right",
        (target_width * 0.5 - frame_thickness * 0.5, 0, 0),
        (frame_thickness, frame_thickness, target_height),
    )
    create_box(
        "Fixed_Center_Mullion",
        (0, 0, 0),
        (mullion_thickness, frame_thickness, target_height - frame_thickness * 2),
    )

    pane_width = (target_width - frame_thickness * 2 - mullion_thickness) * 0.5
    pane_height = target_height - frame_thickness * 2
    left_x = -mullion_thickness * 0.5 - pane_width * 0.5
    right_x = mullion_thickness * 0.5 + pane_width * 0.5

    create_box(
        "Stretch_Glass_Pane_01",
        (left_x, 0, 0),
        (pane_width, glass_thickness, pane_height),
    )
    create_box(
        "Stretch_Glass_Pane_02",
        (right_x, 0, 0),
        (pane_width, glass_thickness, pane_height),
    )
```

중요:

- glass 두께는 항상 고정한다.
- mullion 두께는 항상 고정한다.
- pane이 커지는 영역만 stretch part로 만든다.

## Bevel 규약

bevel은 모델을 보기 좋게 만들지만 과하면 Unity 용량과 vertex 수가 증가한다.

권장:

- frame bevel: `0.004` ~ `0.01`
- metal/rail bevel: `0.003` ~ `0.008`
- glass bevel: 생략하거나 매우 작게
- bevel segments: `1` 또는 `2`

예시:

```python
def add_bevel(obj, amount=0.006, segments=1):
    bevel = obj.modifiers.new("Small_Bevel", "BEVEL")
    bevel.width = amount
    bevel.segments = segments
    bevel.affect = "EDGES"
    obj.modifiers.new("Weighted_Normals", "WEIGHTED_NORMAL")
```

금지:

- 모든 작은 bar에 높은 segment bevel 적용
- 유리 pane에 불필요한 bevel 다량 적용
- export 전에 modifier가 과도하게 남아 vertex 수가 폭증하는 구조

## Material 규약

Blender에서는 Unity에서 교체하기 쉬운 단순 material slot만 분리한다.

권장 material:

- `Door_Frame`
- `Door_Slab`
- `Door_Metal`
- `Window_Frame`
- `Window_Glass`
- `Balcony_Frame`
- `Balcony_Railing`
- `Balcony_Glass`

유리 material은 alpha 값을 둔다.

```python
def create_glass_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.blend_method = "BLEND"
    mat.show_transparent_back = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Alpha"].default_value = 0.28
        bsdf.inputs["Base Color"].default_value = (0.65, 0.85, 1.0, 0.28)
        bsdf.inputs["Roughness"].default_value = 0.08
    return mat
```

주의:

- Unity에서 최종 재질을 단순하게 관리하려면 material 수를 과하게 늘리지 않는다.
- texture 없이 색상/투명도 중심으로 구성한다.
- glass material은 Unity shader에서 transparent 설정이 다시 필요할 수 있다.

## Custom Property 규약

root empty에는 모델 규약을 기록한다.

```python
root["lh_opening_type"] = "Window"
root["lh_target_width"] = target_width
root["lh_target_height"] = target_height
root["lh_target_depth"] = target_depth
root["lh_modeling_rule"] = "fixed_parts_plus_stretch_parts"
root["lh_parametric_profile_key"] = "BALCONY_RAILING_WINDOW_V1"
```

권장 root 이름:

- `Door_Parametric_ROOT`
- `Window_Parametric_ROOT`
- `Balcony_Parametric_ROOT`

## Validation 규약

모델 생성 함수는 target size가 고정 파트를 담기에 너무 작으면 즉시 실패해야 한다.

예시:

```python
min_width = frame_thickness * 2 + mullion_thickness + 0.3
min_height = frame_thickness * 2 + 0.4

if target_width < min_width:
    raise ValueError(f"target_width is too small. min={min_width}, got={target_width}")

if target_height < min_height:
    raise ValueError(f"target_height is too small. min={min_height}, got={target_height}")
```

검증해야 할 항목:

- frame thickness가 target size와 무관하게 유지되는가
- glass thickness가 유지되는가
- rail thickness가 유지되는가
- 손잡이/힌지/lock 크기가 유지되는가
- vertical bar가 scale되지 않고 위치만 재배치되는가
- root scale이 `(1,1,1)`인가
- child object scale도 가능한 `(1,1,1)`로 apply되어 있는가
- object 이름이 `Fixed_` 또는 `Stretch_` 규약을 따르는가

## Export FBX 규약

권장 export 함수:

```python
def export_fbx(filepath):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.name.startswith(("Door_Parametric", "Window_Parametric", "Balcony_Parametric")):
            obj.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        mesh_smooth_type="FACE",
    )
```

Unity import 후 확인:

- `globalScale: 1`
- `useFileUnits: 1`
- `bakeAxisConversion: 1`
- prefab root rotation identity
- prefab root scale `(1,1,1)`

## 전체 예시: Cube Helper

모든 cube는 helper로 생성해 dimension/location/apply scale을 통일한다.

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

    if bevel > 0:
        add_bevel(obj, bevel, 1)

    return obj
```

중요:

- `dimensions` 설정 후 `transform_apply(scale=True)`를 호출한다.
- 최종 object scale이 `(1,1,1)`에 가깝게 남도록 한다.
- location은 target size를 기준으로 매번 재계산한다.

## 금지 패턴 요약

다음 방식은 사용하지 않는다.

- 완성 모델 root를 opening width/height/depth에 맞춰 non-uniform scale
- 하나의 mesh로 frame, glass, rail, handle을 모두 합쳐 만들기
- 손잡이/힌지/난간 bar를 stretch part에 포함
- target width가 커질 때 frame thickness까지 커지게 만들기
- target height가 커질 때 rail thickness까지 커지게 만들기
- prefab root에 축 보정 rotation을 남기기
- Unity catalog에서 `fitWidth/fitHeight/fitDepth=true`로 parametric prefab 전체를 다시 스케일하기
- part 이름을 `Cube.001`, `Rail`, `Glass`처럼 규칙 없이 만들기

## 권장 생성 순서

1. 입력 target size와 고정 치수 validation
2. material 생성
3. root empty 생성
4. fixed frame 생성
5. stretch glass/slab 생성
6. mullion/rail/post/bar 생성
7. handle/hinge/lock 같은 fixed detail 생성
8. bevel/weighted normal 적용
9. root custom property 기록
10. scale apply 확인
11. FBX export
12. Unity import 후 prefab root rotation/scale 확인
13. `OpeningTypeCatalog`에 `fitWidth/fitHeight/fitDepth=false`로 등록
14. runtime에서 `ParametricOpeningModel.ApplyOpeningSize()`로 검증
