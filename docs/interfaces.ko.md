# 인터페이스 정의서

## 1. 목적

본 문서는 LH Editor Refactoring의 외부/내부 인터페이스를 정리합니다. 대상은 Scene wiring, C# 입력/명령 인터페이스, `.lhscene` 저장 schema, 모바일 export schema, FurnitureAuthoringTool manifest입니다.

## 2. Scene Wiring 계약

주요 씬은 `Assets/Scenes/SampleScene.unity`입니다. 씬은 다음 root 또는 역할을 포함해야 합니다.

| 역할 | 설명 |
|---|---|
| `Managers` | `ModeManager`, 입력, 벽/방/가구/저장/export 관련 manager를 배치합니다. |
| `_TopPlanContent` | Top view에서 벽, 방, opening marker, overlay를 표시하는 UI/그래픽 root입니다. |
| `_Walls` | 생성된 벽 GameObject의 기본 parent입니다. |
| `_Rooms` | 생성된 Room GameObject의 기본 parent입니다. |
| `_Screen` | 주요 UI canvas와 버튼을 포함합니다. |
| `Grid` | Top view 편집 기준 grid입니다. |

필수 참조는 가능한 한 `SceneReferenceRegistry`와 Inspector mapping 문서로 관리해야 합니다. Runtime에서 참조를 자동 생성하는 bootstrap은 보조 수단이며, 장기적으로는 명시적 scene wiring을 우선합니다.

## 3. 편집 모드 인터페이스

### `EditorMode`

| 값 | 의미 |
|---:|---|
| 0 | `Default` |
| 1 | `RoomCreate` |
| 3 | `DetailEdit` |
| 6 | `DoorInsert` |
| 7 | `WindowInsert` |
| 9 | `FurniturePlace` |
| 10 | `DrawingOverlayCalibrate` |

Legacy 값 `2`, `4`, `5`, `8`은 `RoomCreate`로 정규화됩니다.

### `EditorViewMode`

| 값 | 의미 |
|---:|---|
| 0 | `Top` |
| 1 | `Perspective3D` |

## 4. 내부 C# 인터페이스

### `IEditorInputProvider`

입력 장치와 Unity EventSystem 접근을 추상화합니다.

```csharp
public interface IEditorInputProvider
{
    bool IsPointerAvailable { get; }
    bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition);
    bool TryGetPointerDelta(out Vector2 pointerDelta);
    float GetScrollDeltaY();
    bool IsPointerOverUI(EventSystem eventSystem, List<RaycastResult> raycastResults = null);
    bool WasPointerButtonPressedThisFrame(PointerButton button);
    bool WasPointerButtonReleasedThisFrame(PointerButton button);
    bool IsPointerButtonPressed(PointerButton button);
    bool WasKeyPressedThisFrame(Key key);
    bool IsKeyPressed(Key key);
}
```

### `IEditorModeInputHandler`

현재 모드가 입력 프레임을 처리하는 계약입니다.

```csharp
public interface IEditorModeInputHandler
{
    void HandleEditorInput(EditorInputFrame inputFrame);
}
```

### `IEditorInputCommand`

입력 프레임에서 파생된 실행 명령입니다.

```csharp
public interface IEditorInputCommand
{
    void Execute();
}
```

### `IEditorCommand`

Undo/Redo 가능한 편집 명령 계약입니다.

```csharp
public interface IEditorCommand
{
    void Execute(UndoRedoManager context);
    void Undo(UndoRedoManager context);
    void Redo(UndoRedoManager context);
}
```

### Wall Tool 내부 계약

`IWallTool`과 `IWallToolContext`는 벽 작성/편집 tool state를 분리합니다. 현재 `internal` 계약이므로 외부 확장 API가 아니라 `Draw/Wall/Core` 내부 구조로 취급합니다.

```csharp
internal interface IWallTool
{
    void Enter();
    void Exit();
    WallToolRequest HandleInput(WallToolInputFrame inputFrame);
}
```

## 5. 작업 상태 저장 Schema

파일 확장자는 `.lhscene`이며 `LhWorkStateDto`를 JSON으로 직렬화합니다.

```json
{
  "version": 1,
  "walls": [],
  "rooms": [],
  "furniture": []
}
```

### `LhWorkWallDto`

| 필드 | 타입 | 설명 |
|---|---|---|
| `id` | string | 작업 상태 내 벽 식별자 |
| `name` | string | 표시 이름 |
| `start`, `end` | vector3 | 벽 시작/끝 좌표 |
| `thickness` | float | 벽 두께 |
| `height` | float | 벽 높이 |
| `centerY` | float | 벽 중심 Y |
| `startVertexId`, `endVertexId` | int | 연결 vertex id |
| `suppressStartHandle`, `suppressEndHandle` | bool | 핸들 표시 억제 여부 |
| `startSplitPoint`, `endSplitPoint` | bool | split point 여부 |
| `openings` | array | 문/창 opening 목록 |

### `LhWorkOpeningDto`

| 필드 | 설명 |
|---|---|
| `type` | opening 종류 |
| `doorTypeKey`, `windowTypeKey`, `prefabKey` | catalog/prefab 식별자 |
| `doorOpensRight`, `doorVerticalFlip` | 문 방향 옵션 |
| `centerDistance`, `width`, `height`, `depth`, `bottomY` | 벽 기준 배치 치수 |

### `LhWorkRoomDto`

| 필드 | 설명 |
|---|---|
| `name`, `roomTypeKey`, `roomCode`, `roomNativeCode` | 방 표시/연동 메타데이터 |
| `floorTextureCode`, `ceilingTextureCode` | 마감 코드 |
| `isManualRoom` | 수동 생성 여부 |
| `placementOffset` | 배치 보정값 |
| `boundaryVertices` | 방 경계 vertex 목록 |
| `wallIds` | 연결 벽 id 목록 |
| `manualWallSelectionEnabled`, `manualWallIds` | 수동 벽 연결 정보 |

### `LhWorkFurnitureDto`

| 필드 | 설명 |
|---|---|
| `catalogCode`, `exportCode`, `nativeCode`, `name` | 가구 식별/연동 코드 |
| `position`, `eulerAngles`, `localScale` | 배치 transform |
| `isPlaced` | 배치 완료 여부 |
| `roomName` | 귀속 방 이름 |

## 6. 모바일 Export Schema

Export DTO는 `Assets/Scripts/Export/LhSceneSchema.cs`의 `LH.Schema` 네임스페이스에 정의됩니다.

### Top-level

```json
{
  "version": 2,
  "startPoint": { "x": 0, "y": 0, "z": 0 },
  "wallData": [],
  "roomData": []
}
```

Legacy exact 모드는 `version` 없이 `startPoint`, `wallData`, `roomData`를 유지할 수 있습니다.

### `wallData`

| 필드 | 설명 |
|---|---|
| `name` | 벽 이름 |
| `id` | export 내 벽 id |
| `position`, `angle`, `scale` | 벽 transform |
| `segments` | 벽 segment 목록 |

`segments[*]`는 `position`, `angle`, `scale`, `hasInterior`, `door`, `window`를 포함합니다.

### `roomData`

| 필드 | 설명 |
|---|---|
| `id` | Extended 모드 room id |
| `name` | 방 이름 |
| `code` | legacy `mntnSpceCd` 대응 코드 |
| `roomTypeKey` | 내부 room type key |
| `nativeCode` | 외부 연동 방 코드 |
| `position`, `angle`, `scale` | transform |
| `walls` | 소속 벽 id 목록 |
| `floor`, `ceil` | 바닥/천장 surface |
| `furnish` | 가구 목록 |

### `furnish`

| 필드 | 설명 |
|---|---|
| `name` | 표시 이름 |
| `code` | legacy 가구 코드 |
| `nativeCode` | 외부 연동 코드 |
| `position`, `angle`, `scale` | 배치 transform |
| `defects` | `mntnCd`, `locCd`, `mtrlCd` tuple 목록 |

## 7. Furniture Manifest 계약

FurnitureAuthoringTool manifest는 최종 런타임 패치 파일이 아니라 patch build 입력입니다.

```json
{
  "manifestVersion": 1,
  "catalogVersion": "2026.07",
  "createdAt": "2026-07-09T00:00:00Z",
  "author": "LH",
  "items": []
}
```

`items[*]` 필수 필드:

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

`defects[*]`는 `mntnCd`, `locCd`, `mtrlCd`를 가져야 하며 공백이면 validation 오류입니다.

## 8. 오류 처리 원칙

- import, load, export 실패는 기존 씬 상태를 보존해야 합니다.
- 사용자가 수정할 수 있는 오류는 파일명, 누락 필드, 대상 object 이름을 포함해 보고해야 합니다.
- 지원하지 않는 schema version은 자동 보정하지 않고 명시적으로 거부합니다.
- Export validation은 누락 code, 빈 geometry, unresolved prefab, invalid defect reference를 감지해야 합니다.
