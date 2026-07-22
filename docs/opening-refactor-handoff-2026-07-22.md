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
  - `ParametricOpeningModel.ApplyProfileMaterials` is a thin adapter that walks cached parts and applies resolved materials.
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
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.ProfileClassification.cs`
  - Moved profile classification helpers from `ParametricOpeningModel.cs` into a focused partial without behavior changes.
  - Extracted methods:
    - `NormalizeProfileKey`
    - `IsDoorProfile`
    - `IsSingleEntranceDoorProfile`
    - `IsEntranceDoorSidePanelProfile`
    - `IsWindowProfile`
    - `ContainsAny`
  - Similar private helper methods still exist inside top-level helper classes such as `ParametricOpeningPartTransformRules`, `ParametricOpeningMaterialRules`, and authored-size helpers. These were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.MaterialRules.cs`
  - Moved `ApplyProfileMaterials` adapter from `ParametricOpeningModel.cs` into this material-focused partial without behavior changes.
  - `ParametricOpeningMaterialRules` still owns material selection and material creation.
- `Assets/Scripts/Draw/Wall/Opening/OpeningTypeResolver.cs`
  - Added an internal static helper for opening catalog lookup.
  - Moved definition lookup by `OpeningPlacementType` and type/display key out of `WallOpeningPlacementManager.TypeCatalog.cs`.
  - Added `ResolveTypeKey(WallOpening)` for the existing door/window key selection logic.
  - `WallOpeningPlacementManager.TypeCatalog.cs` still owns catalog loading, option cache construction, and UI option application.
- `Assets/Scripts/Draw/Wall/Opening/OpeningLayoutService.cs`
  - Added an internal static helper for opening layout constraint calculations.
  - Moved center-distance, width, height, and bottom-offset clamping calculations out of `WallOpeningPlacementManager.Rules.cs`.
  - `WallOpeningPlacementManager.Rules.cs` remains a thin adapter that collects current openings, resolves catalog constraints, and preserves existing private method call sites.
  - No behavior changes were intended.
- `Assets/Scripts/Draw/Wall/Opening/OpeningVisualResources.cs`
  - Added an internal helper for cached cube mesh creation, placeholder material creation, and material cleanup.
  - Removed direct cube mesh/material cache fields from `WallOpeningPlacementManager`.
  - `WallOpeningPlacementManager.Visuals.cs` now delegates visual resource lookup to the helper while keeping opening transform/model/filler orchestration unchanged.
  - Existing wall/segment creation paths now request the cube mesh through `GetCubeMesh()` instead of reading a field directly.
  - No behavior changes were intended.
- `Assets/Scripts/Draw/Wall/Opening/OpeningPlaceholderVisualBuilder.cs`
  - Added an internal static helper for opening placeholder component setup.
  - Moved `MeshFilter`, `BoxCollider`, and `MeshRenderer` ensure/apply logic out of `WallOpeningPlacementManager.Visuals.cs`.
  - `WallOpeningPlacementManager.Visuals.cs` still owns opening transform placement, catalog/model-prefab decision flow, filler segment creation, and marker creation.
  - No behavior changes were intended.
- `Assets/Scripts/Draw/Wall/Opening/OpeningModelPrefabApplier.cs`
  - Added an internal static helper for applying or clearing opening model prefabs from `OpeningTypeCatalogItem`.
  - Moved the `OpeningTypeCatalogItem` to `WallOpening.ApplyModelPrefab` field mapping out of `WallOpeningPlacementManager.Visuals.cs`.
  - `WallOpeningPlacementManager.Visuals.cs` still owns catalog definition lookup and placeholder visibility decision.
  - No behavior changes were intended.
- `Assets/Scripts/Draw/Wall/Opening/OpeningVisualTransformBuilder.cs`
  - Added an internal static helper for applying opening parent, position, rotation, and scale.
  - Moved segment-root and standalone opening transform placement out of `WallOpeningPlacementManager.Visuals.cs`.
  - `WallOpeningPlacementManager.Visuals.cs` uses the returned parent for existing filler segment creation.
  - No behavior changes were intended.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `RefreshOpeningSelectionPresentation`.
  - Centralized the repeated detail menu refresh, detail input refresh, and marker dirty calls used by `SelectOpening` and `ClearOpeningSelection`.
  - Selection state changes, delete behavior, marker drag behavior, undo recording, and rebuild flow were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `DetachAndDestroyOpening`.
  - Moved the selected-opening detach, deactivate, and destroy sequence out of `DeleteSelectedOpening`.
  - Container-empty restore, container rebuild, wall selection refresh, snapshot capture, and undo recording were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `CapturePostDeleteLayoutSnapshot`.
  - Moved the post-delete choice between restored-wall and container layout snapshot capture out of `DeleteSelectedOpening`.
  - Container-empty restore, container rebuild, wall selection refresh, and undo recording were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `RecordOpeningDeleteUndo`.
  - Moved the delete undo null-check and `RecordOpeningLayoutChange` call out of `DeleteSelectedOpening`.
  - Delete flow state changes and snapshot capture were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `SetMarkerUIRegistered`.
  - Centralized marker UI register/unregister null checks, add/remove calls, and marker dirty marking.
  - Marker registration public API and marker refresh behavior were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `RebuildContainerAfterOpeningDelete`.
  - Moved the post-delete container rebuild and selected-wall refresh path out of `DeleteSelectedOpening`.
  - Restored-wall handling, snapshot capture, and undo recording were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Selection.cs`
  - Added `SelectRestoredWallAfterOpeningDelete`.
  - Moved the restored-wall selection-manager null check and selection call out of `DeleteSelectedOpening`.
  - Container rebuild, snapshot capture, and undo recording were left unchanged.
- `Assets/Scripts/Draw/Wall/Opening/OpeningLayoutService.cs`
  - Added `TryResolveNewOpeningSpan`.
  - Moved the gap selection and default new-opening span calculation out of `WallOpeningPlacementManager.LayoutSnapshots.cs`.
  - `WallOpeningPlacementManager.LayoutSnapshots.cs` still owns opening collection/sorting, default size resolution, object creation, rebuild, UI selection, undo recording, and wall registry refresh.
  - No behavior changes were intended.

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
- Unity script compilation succeeded after moving profile classification helpers into `ParametricOpeningModel.ProfileClassification.cs`.
- Unity script compilation succeeded after moving `ApplyProfileMaterials` into `ParametricOpeningModel.MaterialRules.cs`.
- Unity script compilation succeeded after extracting `OpeningTypeResolver`.
- EditMode test execution issue was diagnosed.
  - Do not pass `-quit` with `-runTests`; Unity Test Framework schedules the run after editor startup and exits itself when the run completes.
  - Use a project-relative `-testResults` path such as `Logs\test-results.xml`.
  - With this corrected invocation, XML results are created.
- An earlier transform-rules test retry hit Unity's `ProjectAlreadyOpenInAnotherInstance` crash path. Treat that as an environment/Unity process state issue, not a test assertion result.
- Earlier targeted test commands used `-quit`, so they exited after script compilation without running tests or writing result XML.
- After the part-transform partial split, a targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-state partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the entrance-door layout partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the white-window layout partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-lookup partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the part-cache partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the size-resolution partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the profile-classification partial split, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- After the material-adapter move, the same targeted EditMode test command again produced no result XML. The Unity log showed script compilation and clean batchmode exit, but no test execution/result output.
- Corrected Test Runner verification:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningTypeResolver`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningLayoutService`, Unity script compilation succeeded.
- After extracting `OpeningLayoutService`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningVisualResources`, Unity script compilation succeeded.
- After extracting `OpeningVisualResources`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningPlaceholderVisualBuilder`, Unity script compilation succeeded.
- After extracting `OpeningPlaceholderVisualBuilder`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningModelPrefabApplier`, Unity script compilation succeeded.
- After extracting `OpeningModelPrefabApplier`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting `OpeningVisualTransformBuilder`, Unity script compilation succeeded.
- After extracting `OpeningVisualTransformBuilder`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After centralizing opening selection presentation refresh, Unity script compilation succeeded.
- After centralizing opening selection presentation refresh, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting the opening detach/destroy helper, Unity script compilation succeeded.
- After extracting the opening detach/destroy helper, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting the post-delete snapshot helper, Unity script compilation succeeded.
- After extracting the post-delete snapshot helper, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting the opening delete undo helper, Unity script compilation succeeded.
- After extracting the opening delete undo helper, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After centralizing marker UI registration, Unity script compilation succeeded.
- After centralizing marker UI registration, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting the opening delete rebuild helper, Unity script compilation succeeded.
- After extracting the opening delete rebuild helper, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting the restored-wall selection helper, Unity script compilation succeeded.
- After extracting the restored-wall selection helper, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
- After extracting new-opening span calculation into `OpeningLayoutService`, Unity script compilation succeeded.
- After extracting new-opening span calculation into `OpeningLayoutService`, corrected Test Runner verification passed:
  - `OpeningTypeCatalogTests` passed: 4 total, 4 passed, 0 failed.
  - `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passed: 1 total, 1 passed, 0 failed.
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
  - `Logs/parametric-opening-profile-classification-compile.log`
  - `Logs/parametric-opening-profile-classification-test.log`
  - `Logs/parametric-opening-material-adapter-compile.log`
  - `Logs/parametric-opening-material-adapter-test.log`
  - `Logs/test-runner-opening-catalog-no-quit.log`
  - `Logs/test-runner-opening-catalog-relative-no-quit.log`
  - `Logs/test-runner-opening-catalog-relative-no-quit.xml`
  - `Logs/wall-opening-placement-rail-regression-no-quit.log`
  - `Logs/wall-opening-placement-rail-regression-no-quit.xml`
  - `Logs/opening-type-resolver-compile.log`
  - `Logs/opening-type-resolver-catalog-tests.log`
  - `Logs/opening-type-resolver-catalog-tests.xml`
  - `Logs/opening-type-resolver-rail-regression.log`
  - `Logs/opening-type-resolver-rail-regression.xml`
  - `Logs/opening-layout-service-compile.log`
  - `Logs/opening-layout-service-final-compile.log`
  - `Logs/opening-layout-service-catalog-tests.log`
  - `Logs/opening-layout-service-catalog-tests.xml`
  - `Logs/opening-layout-service-rail-regression.log`
  - `Logs/opening-layout-service-rail-regression.xml`
  - `Logs/opening-visual-resources-compile.log`
  - `Logs/opening-visual-resources-catalog-tests.log`
  - `Logs/opening-visual-resources-catalog-tests.xml`
  - `Logs/opening-visual-resources-rail-regression.log`
  - `Logs/opening-visual-resources-rail-regression.xml`
  - `Logs/opening-placeholder-visual-builder-compile.log`
  - `Logs/opening-placeholder-visual-builder-catalog-tests.log`
  - `Logs/opening-placeholder-visual-builder-catalog-tests.xml`
  - `Logs/opening-placeholder-visual-builder-rail-regression.log`
  - `Logs/opening-placeholder-visual-builder-rail-regression.xml`
  - `Logs/opening-model-prefab-applier-compile.log`
  - `Logs/opening-model-prefab-applier-catalog-tests.log`
  - `Logs/opening-model-prefab-applier-catalog-tests.xml`
  - `Logs/opening-model-prefab-applier-rail-regression.log`
  - `Logs/opening-model-prefab-applier-rail-regression.xml`
  - `Logs/opening-visual-transform-builder-compile.log`
  - `Logs/opening-visual-transform-builder-catalog-tests.log`
  - `Logs/opening-visual-transform-builder-catalog-tests.xml`
  - `Logs/opening-visual-transform-builder-rail-regression.log`
  - `Logs/opening-visual-transform-builder-rail-regression.xml`
  - `Logs/opening-selection-presentation-compile.log`
  - `Logs/opening-selection-presentation-catalog-tests.log`
  - `Logs/opening-selection-presentation-catalog-tests.xml`
  - `Logs/opening-selection-presentation-rail-regression.log`
  - `Logs/opening-selection-presentation-rail-regression.xml`
  - `Logs/opening-detach-destroy-compile.log`
  - `Logs/opening-detach-destroy-catalog-tests.log`
  - `Logs/opening-detach-destroy-catalog-tests.xml`
  - `Logs/opening-detach-destroy-rail-regression.log`
  - `Logs/opening-detach-destroy-rail-regression.xml`
  - `Logs/opening-post-delete-snapshot-compile.log`
  - `Logs/opening-post-delete-snapshot-catalog-tests.log`
  - `Logs/opening-post-delete-snapshot-catalog-tests.xml`
  - `Logs/opening-post-delete-snapshot-rail-regression.log`
  - `Logs/opening-post-delete-snapshot-rail-regression.xml`
  - `Logs/opening-delete-undo-helper-compile.log`
  - `Logs/opening-delete-undo-helper-catalog-tests.log`
  - `Logs/opening-delete-undo-helper-catalog-tests.xml`
  - `Logs/opening-delete-undo-helper-rail-regression.log`
  - `Logs/opening-delete-undo-helper-rail-regression.xml`
  - `Logs/opening-marker-registration-helper-compile.log`
  - `Logs/opening-marker-registration-helper-catalog-tests.log`
  - `Logs/opening-marker-registration-helper-catalog-tests.xml`
  - `Logs/opening-marker-registration-helper-rail-regression.log`
  - `Logs/opening-marker-registration-helper-rail-regression.xml`
  - `Logs/opening-delete-rebuild-helper-compile.log`
  - `Logs/opening-delete-rebuild-helper-catalog-tests.log`
  - `Logs/opening-delete-rebuild-helper-catalog-tests.xml`
  - `Logs/opening-delete-rebuild-helper-rail-regression.log`
  - `Logs/opening-delete-rebuild-helper-rail-regression.xml`
  - `Logs/opening-delete-restored-wall-selection-compile.log`
  - `Logs/opening-delete-restored-wall-selection-catalog-tests.log`
  - `Logs/opening-delete-restored-wall-selection-catalog-tests.xml`
  - `Logs/opening-delete-restored-wall-selection-rail-regression.log`
  - `Logs/opening-delete-restored-wall-selection-rail-regression.xml`
  - `Logs/opening-span-layout-service-compile.log`
  - `Logs/opening-span-layout-service-catalog-tests.log`
  - `Logs/opening-span-layout-service-catalog-tests.xml`
  - `Logs/opening-span-layout-service-rail-regression.log`
  - `Logs/opening-span-layout-service-rail-regression.xml`

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

### 1. Verify tests with the corrected Unity Test Runner command

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testFilter OpeningTypeCatalogTests -testResults 'Logs\opening-catalog-tests.xml' -logFile 'E:\Unity\LH Editor_Refactoring\Logs\opening-catalog-tests.log'
```

Expected:

- result XML is created
- `OpeningTypeCatalogTests` pass
- `WallOpeningPlacementManagerTests.ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize` passes

Important:

- Do not include `-quit` in `-runTests` commands.
- Prefer a project-relative `-testResults` path under `Logs\`.

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
- Profile classification helpers were moved into `ParametricOpeningModel.ProfileClassification.cs`.
- Material application adapter was moved into `ParametricOpeningModel.MaterialRules.cs`.
- `ParametricOpeningModel.cs` is now about 109 lines.
- `PartState` remains private.

Next extraction:

- Pause simple `ParametricOpeningModel.cs` file-splitting for now. The remaining main file is mostly public entrypoints, serialized fields/constants, and high-level dispatch.
- Do not start deeper behavior-oriented profile dispatch rewrites unless the relevant EditMode tests pass with the corrected command.
- Recommended next direction:
  - move to the next high-priority large class, `WallOpeningPlacementManager`, with the same small, behavior-preserving extraction style
  - first inspect its partial files and identify type/catalog/modelLayout/collisionPolicy resolution methods for a narrow `OpeningTypeResolver` extraction

Do not do a large rewrite in one step. First move code with minimal behavior change.

### 4. Prepare `WallOpeningPlacementManager` split

Do after tests are stable.

Suggested extraction order:

1. `OpeningTypeResolver` - started with catalog definition lookup and door/window key resolution.
2. `OpeningLayoutService` - started with opening clamp calculations and new-opening span calculation.
3. `OpeningVisualBuilder` - started with visual resource cache, placeholder component setup, model prefab application, and visual transform extraction.
4. `OpeningSelectionController` - started with selection presentation refresh cleanup, detach/destroy extraction, post-delete snapshot extraction, delete undo extraction, marker registration cleanup, delete rebuild extraction, and restored-wall selection extraction.

Keep `WallOpeningPlacementManager` as Unity lifecycle/input orchestration.

## Suggested First Message For A New Chat

```text
이 Unity 프로젝트에서 개구부 모델 리팩터링을 이어서 진행하자.
먼저 docs/opening-refactor-handoff-2026-07-22.md 를 읽고 현재 상태를 파악해줘.
그 다음 ParametricOpeningModel 축소 1단계로 profile key 상수 분리부터 진행해줘.
테스트 실행은 Unity batchmode가 현재 -runTests를 제대로 실행하지 않는 문제가 있었고, 컴파일은 성공했어.
```
