# Opening Refactor Handoff - 2026-07-22

## Current Goal

Reduce risk and code growth around opening models, especially `OpeningTypeCatalog`, `ParametricOpeningModel`, and `WallOpeningPlacementManager`.

## Completed In This Session

- `S003` was confirmed as a window-like pass-through opening.
- `Assets/Resources/OpeningTypeCatalog.asset`
  - `S003` changed to `openingType: 1` (`Window`).
  - `S003` keeps `modelLayoutKind: 2` (`WindowLike`).
  - `S003` keeps `collisionPolicy: 1` (`PassThrough`).
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.cs`
  - Added `WhiteSwingDoorProfile = "WHITE_SWING_DOOR_V1"`.
  - Added runtime dispatch for `WHITE_SWING_DOOR_V1`.
  - Added `ApplyWhiteSwingDoorLayout`.
  - Added `ResolveWhiteSwingDoorAuthoredPartSize`.
- `Assets/Tests/Editor/OpeningTypeCatalogTests.cs`
  - Added catalog validation tests.
  - Tests use reflection because `LH.Editor.Tests.asmdef` does not directly reference `Assembly-CSharp`.
  - Coverage:
    - duplicate `typeKey` detection
    - supported parametric profile keys
    - positive reference/authored sizes
    - valid min/max size ranges
    - `S003` is `Window + WindowLike + PassThrough`

## Completed In Follow-up Session

- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteDoorAuthoredSizes.cs`
  - Fixed `WHITE_GLASS_FRAME_DOOR_V1` authored-size resolution for `Stretch_Top_Rail` and `Stretch_Bottom_Rail`.
  - These rail parts now resolve from the authored rail size instead of falling back to `MinimumSize`.
- `Assets/Scripts/Editor/OpeningModelViewerSyncWindow.cs`
  - Updated runtime script sync to copy all `ParametricOpeningModel*.cs` partial files.
  - The viewer main script `.meta` GUID is still preserved because existing viewer prefabs reference it.
  - Partial script `.meta` files are copied with their source files.
- `Assets/Tests/Editor/WallOpeningPlacementManagerTests.cs`
  - Added a regression test for `WhiteGlassFrameDoor` rail scaling.
- `Assets/Scripts/Furniture/FurniturePlacementManager.cs`
  - Disabled forced furniture placement validation logs by default.
  - `enableValidationDebugLogs` now controls normal logging behavior unless `ForceValidationDebugLogs` is intentionally re-enabled in code.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteDoorLayouts.cs`
  - Reduced this partial to thin adapter methods.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteDoorLayoutRules.cs`
  - Extracted white door layout calculations into `WhiteDoorOpeningLayout`.
  - `ParametricOpeningModel` still owns part lookup and transform application through the existing `ApplyKnownPart` callback.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteDoorAuthoredSizes.cs`
  - Replaced the nested `PartState` partial with an internal static `WhiteDoorOpeningPartSizes` helper.
  - `PartState.ResolveAuthoredPartSize` still owns dispatch and calls the helper for white door profiles.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.EntranceDoorAuthoredSizes.cs`
  - Extracted entrance-door authored-size rules into `EntranceDoorOpeningPartSizes`.
  - `PartState.ResolveAuthoredPartSize` still owns dispatch and calls this helper for single entrance, lockset, and side-panel entrance door profiles.

## Verification State

- Unity script compilation succeeded after the reflection-based test adjustment.
- Unity script compilation also succeeded after the follow-up changes.
- Unity script compilation succeeded after extracting `WhiteDoorOpeningLayout`.
- Unity script compilation succeeded after extracting `WhiteDoorOpeningPartSizes`.
- Unity script compilation succeeded after extracting `EntranceDoorOpeningPartSizes`.
- EditMode test execution did not produce result XML in this environment.
- The same issue occurred with a previously passing filter, `WallOpeningPlacementManagerTests`, so this appears to be a current Unity Test Runner/batchmode invocation issue, not a new test failure.
- Relevant logs:
  - `Logs/opening-catalog-tests.log`
  - `Logs/wall-opening-placement-tests-rerun.log`
  - `Logs/wall-opening-placement-manager-tests.log`
  - `Logs/wall-opening-placement-manager-tests-rerun.log`
  - `Logs/white-door-layout-helper-compile.log`
  - `Logs/white-door-layout-helper-test.log`
  - `Logs/white-door-authored-size-helper-compile.log`
  - `Logs/white-door-authored-size-helper-test.log`
  - `Logs/entrance-door-authored-size-helper-compile.log`
  - `Logs/entrance-door-authored-size-helper-test.log`

## Important Context

The user clarified:

- `S003` is a 창호/window-like opening.
- It is pass-through because a person can pass through it.
- Actual semantic classification and model generation layout can be different concepts.

Current code does not fully separate those concepts:

- `OpeningPlacementType` controls UI grouping, key selection, catalog lookup, and export `door/window` fields.
- `OpeningModelLayoutKind` should represent generation/layout behavior.
- `OpeningCollisionPolicy` should represent pass-through/blocking behavior.

Recommended direction:

- Treat `openingType` as editor/export category for now.
- Treat `modelLayoutKind` as model-generation behavior.
- Treat `collisionPolicy` as navigation/collision behavior.
- Longer term, consider renaming or splitting `OpeningPlacementType` if it continues to mean too many things.

## Next Recommended Work

### 1. Verify tests when Unity Test Runner works

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testFilter OpeningTypeCatalogTests -testResults 'Logs\opening-catalog-tests.xml' -logFile 'Logs\opening-catalog-tests.log' -quit
```

Expected:

- result XML is created
- `OpeningTypeCatalogTests` pass
- `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passes

### 2. High-priority improvement points

These are the highest-risk code health items found during project review:

- `ParametricOpeningModel`
  - Current total: about 1666 lines across 3 partial files.
  - Problem: profile dispatch, layout placement, authored part sizes, material rules, stretch rules, and name matching are still one conceptual class.
  - Risk: every new door/window type adds more hardcoded branches and name-fragile rules.
- `WallOpeningPlacementManager`
  - Current total: about 2547 lines across 8 partial files.
  - Problem: placement, layout snapshots, UI, selection, visuals, type catalog resolution, and undo recording are strongly coupled.
  - Risk: split/drag/opening delete behavior can regress because state transitions cross partial-file boundaries.
- `HandleManager`
  - Current total: about 3129 lines across 5 partial files.
  - Problem: handle input, drag rules, registry refresh, UI state, and container drag behavior are mixed.
  - Risk: wall vertex drag, split point drag, opening container rebuild, and undo capture are tightly coupled.
- `FurniturePlacementManager`
  - Current total: about 1075 lines in one file.
  - Problem: input, placement validation, room containment, overlap checks, selection, deletion, preview tinting, and debug logging live together.
  - Risk: validation and input changes can easily affect selection and mode transitions.
- `OpeningModelViewerSyncWindow`
  - Problem: viewer sync is file-system based and depends on exact script/prefab GUID behavior.
  - Risk: future partial files or generated dependencies can be omitted unless sync rules stay explicit.

### 3. Continue shrinking `ParametricOpeningModel`

Current problem:

- `ParametricOpeningModel.cs` is still about 1393 lines.
- New model profiles are implemented as hardcoded C# branches.
- This will keep growing with every new door/window model.

Already done:

- `ParametricOpeningProfileKeys.cs` exists.
- `ParametricOpeningModel` keeps compatibility aliases for profile keys.
- White door layout calculations were extracted to `WhiteDoorOpeningLayout`.
- White door authored-size rules were extracted to `WhiteDoorOpeningPartSizes`.
- Entrance-door authored-size rules were extracted to `EntranceDoorOpeningPartSizes`.

Next extraction:

- Move white two-panel window authored-size rules out of nested `PartState`.
- Preferred shape:
  - an internal static helper such as `WhiteWindowOpeningPartSizes`
  - methods:
    - `ResolveTwoPanelWindow`
  - keep `PartState.ResolveAuthoredPartSize` as the dispatcher until tests are stable.

Do not do a large rewrite in one step. First move code with minimal behavior change.

### 4. Prepare `WallOpeningPlacementManager` split

Do after tests are stable.

Suggested extraction order:

1. `OpeningTypeResolver`
2. `OpeningVisualBuilder`
3. `OpeningLayoutService`
4. `OpeningSelectionController`

Keep `WallOpeningPlacementManager` as Unity lifecycle/input orchestration.

## Suggested First Message For A New Chat

```text
이 Unity 프로젝트에서 개구부 모델 리팩터링을 이어서 진행하자.
먼저 docs/opening-refactor-handoff-2026-07-22.md 를 읽고 현재 상태를 파악해줘.
그 다음 ParametricOpeningModel 축소 1단계로 profile key 상수 분리부터 진행해줘.
테스트 실행은 Unity batchmode가 현재 -runTests를 제대로 실행하지 않는 문제가 있었고, 컴파일은 성공했어.
```
