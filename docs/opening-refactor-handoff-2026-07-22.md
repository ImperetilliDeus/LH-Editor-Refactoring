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
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteWindowAuthoredSizes.cs`
  - Extracted white two-panel window authored-size rules into `WhiteWindowOpeningPartSizes`.
  - `PartState.ResolveAuthoredPartSize` still owns dispatch and calls this helper for `WHITE_TWO_PANEL_WINDOW_V1`.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.MaterialRules.cs`
  - Extracted profile material resolution, generated material caching, resource material loading, and shader selection into `ParametricOpeningMaterialRules`.
  - `ParametricOpeningModel.ApplyProfileMaterials` remains as a thin adapter that walks cached parts and applies resolved materials.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.TransformRules.cs`
  - Extracted stretch and axis-anchor rules into `ParametricOpeningPartTransformRules`.
  - Moved `AxisAnchor` from a private nested enum to an internal top-level enum so the helper can return the same rule result type.
  - `PartState` construction still owns state capture and now delegates stretch/anchor decisions to the helper.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.RailingLayout.cs`
  - Moved balcony railing vertical bar activation, spacing, and positioning into a focused partial.
  - This remains a partial instead of an external helper because it needs access to private cached `PartState` collections.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.PartTransforms.cs`
  - Moved general part transform application into a focused partial without behavior changes.
  - Extracted methods:
    - `ApplyPart`
    - `ResolveScaleForSize`
    - `ResolveScaleAxis`
    - `ResolveNextSize`
    - `ResolveNextAxisSize`
    - `ResolveStretchScale`
    - `ResolveAxisPosition`
  - `PartState` remains a private nested type.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.PartState.cs`
  - Moved the private nested `PartState` type into its own partial file without behavior changes.
  - Authored-size dispatch and approximate mesh-size fallback remain inside `PartState`.
  - `PartState` remains private.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.EntranceDoorLayouts.cs`
  - Moved `ApplySingleEntranceDoorLayout` into a focused partial without behavior changes.
  - The method still uses `ApplyKnownPart` for part lookup and transform application.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.WhiteWindowLayouts.cs`
  - Moved `ApplyWhiteTwoPanelWindowLayout` into a focused partial without behavior changes.
  - The method still uses `ApplyKnownPart` for part lookup and transform application.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.PartLookup.cs`
  - Moved `ApplyKnownPart` and `FindPart` into a focused partial without behavior changes.
  - Layout partials continue to use this private adapter for part lookup and transform application.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.PartCache.cs`
  - Moved `EnsureCached` into a focused partial without behavior changes.
  - Parametric part discovery, `PartState` construction, and railing vertical bar cache sorting remain unchanged.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.SizeResolution.cs`
  - Moved size/reference conversion helpers into a focused partial without behavior changes.
  - Extracted methods:
    - `ClampSize`
    - `ToModelLocalSize`
    - `ResolveReferenceSize`
    - `IsDefaultReferenceSize`
    - `ToRootLocalSize`

## Verification State

- Unity script compilation succeeded after the reflection-based test adjustment.
- Unity script compilation also succeeded after the follow-up changes.
- Unity script compilation succeeded after extracting `WhiteDoorOpeningLayout`.
- Unity script compilation succeeded after extracting `WhiteDoorOpeningPartSizes`.
- Unity script compilation succeeded after extracting `EntranceDoorOpeningPartSizes`.
- Unity script compilation succeeded after extracting `WhiteWindowOpeningPartSizes`.
- Unity script compilation succeeded after extracting `ParametricOpeningMaterialRules`.
- Unity script compilation succeeded after extracting `ParametricOpeningPartTransformRules`.
- Unity script compilation succeeded after moving railing vertical bar layout into `ParametricOpeningModel.RailingLayout.cs`.
- Unity script compilation succeeded after moving general part transform application into `ParametricOpeningModel.PartTransforms.cs`.
- Unity script compilation succeeded after moving private `PartState` into `ParametricOpeningModel.PartState.cs`.
- Unity script compilation succeeded after moving single entrance door layout into `ParametricOpeningModel.EntranceDoorLayouts.cs`.
- Unity script compilation succeeded after moving white two-panel window layout into `ParametricOpeningModel.WhiteWindowLayouts.cs`.
- Unity script compilation succeeded after moving part lookup/application adapters into `ParametricOpeningModel.PartLookup.cs`.
- Unity script compilation succeeded after moving part cache construction into `ParametricOpeningModel.PartCache.cs`.
- Unity script compilation succeeded after moving size/reference conversion helpers into `ParametricOpeningModel.SizeResolution.cs`.
- EditMode test execution did not produce result XML in this environment.
- The transform-rules test retry hit Unity's `ProjectAlreadyOpenInAnotherInstance` crash path. Treat this as an environment/Unity process state issue, not a test assertion result.
- The same issue occurred with a previously passing filter, `WallOpeningPlacementManagerTests`, so this appears to be a current Unity Test Runner/batchmode invocation issue, not a new test failure.
- After the part-transform partial split, a targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-state partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the entrance-door layout partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the white-window layout partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-lookup partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-cache partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the size-resolution partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
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
  - `Logs/white-window-authored-size-helper-compile.log`
  - `Logs/white-window-authored-size-helper-test.log`
  - `Logs/parametric-opening-material-rules-compile.log`
  - `Logs/parametric-opening-material-rules-test.log`
  - `Logs/parametric-opening-transform-rules-compile.log`
  - `Logs/parametric-opening-transform-rules-test.log`
  - `Logs/parametric-opening-railing-layout-compile.log`
  - `Logs/parametric-opening-railing-layout-test.log`
  - `Logs/parametric-opening-part-transforms-compile.log`
  - `Logs/parametric-opening-part-transforms-test.log`
  - `Logs/parametric-opening-part-state-compile.log`
  - `Logs/parametric-opening-part-state-test.log`
  - `Logs/parametric-opening-entrance-layouts-compile.log`
  - `Logs/parametric-opening-entrance-layouts-test.log`
  - `Logs/parametric-opening-white-window-layouts-compile.log`
  - `Logs/parametric-opening-white-window-layouts-test.log`
  - `Logs/parametric-opening-part-lookup-compile.log`
  - `Logs/parametric-opening-part-lookup-test.log`
  - `Logs/parametric-opening-part-cache-compile.log`
  - `Logs/parametric-opening-part-cache-test.log`
  - `Logs/parametric-opening-size-resolution-compile.log`
  - `Logs/parametric-opening-size-resolution-test.log`

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
- White two-panel window authored-size rules were extracted to `WhiteWindowOpeningPartSizes`.
- Profile material rules were extracted to `ParametricOpeningMaterialRules`.
- Stretch and axis-anchor rules were extracted to `ParametricOpeningPartTransformRules`.
- Railing vertical bar layout was moved to `ParametricOpeningModel.RailingLayout.cs`.

Recent extraction:

- General part transform application was moved into `ParametricOpeningModel.PartTransforms.cs`.
- Private `PartState` support was moved into `ParametricOpeningModel.PartState.cs`.
- Single entrance door layout was moved into `ParametricOpeningModel.EntranceDoorLayouts.cs`.
- White two-panel window layout was moved into `ParametricOpeningModel.WhiteWindowLayouts.cs`.
- Part lookup/application adapters were moved into `ParametricOpeningModel.PartLookup.cs`.
- Part cache construction was moved into `ParametricOpeningModel.PartCache.cs`.
- Size/reference conversion helpers were moved into `ParametricOpeningModel.SizeResolution.cs`.
- `ParametricOpeningModel.cs` is now about 179 lines.
- `PartState` remains private.

Next extraction:

- Continue reducing `ParametricOpeningModel.cs` with minimal behavior change.
- Good next targets:
  - consider moving profile classification helpers such as `NormalizeProfileKey`, `IsDoorProfile`, `IsWindowProfile`, and `ContainsAny` into a focused partial
  - consider moving material application adapter `ApplyProfileMaterials` into `ParametricOpeningModel.MaterialRules.cs` or a focused partial

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
