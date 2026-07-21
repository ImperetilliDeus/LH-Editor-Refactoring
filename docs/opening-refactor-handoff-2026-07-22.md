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

## Verification State

- Unity script compilation succeeded after the reflection-based test adjustment.
- EditMode test execution did not produce result XML in this environment.
- The same issue occurred with a previously passing filter, `WallOpeningPlacementManagerTests`, so this appears to be a current Unity Test Runner/batchmode invocation issue, not a new test failure.
- Relevant logs:
  - `Logs/opening-catalog-tests.log`
  - `Logs/wall-opening-placement-tests-rerun.log`

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

### 2. Start shrinking `ParametricOpeningModel`

Current problem:

- `ParametricOpeningModel.cs` is about 1800+ lines.
- New model profiles are implemented as hardcoded C# branches.
- This will keep growing with every new door/window model.

Low-risk first extraction:

- Create `ParametricOpeningProfileKeys.cs` or `OpeningProfileKeyRegistry.cs`.
- Move profile key constants out of `ParametricOpeningModel`.
- Keep compatibility aliases in `ParametricOpeningModel` temporarily if needed.

Next extraction:

- Extract white door layout methods into a focused helper, for example:
  - `WhiteDoorOpeningLayout.cs`
  - static methods:
    - `ApplyOneSlidingDoor`
    - `ApplySwingDoor`
    - `ApplyGlassFrameDoor`
    - authored-size resolvers for those profiles

Do not do a large rewrite in one step. First move code with minimal behavior change.

### 3. Prepare `WallOpeningPlacementManager` split

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
