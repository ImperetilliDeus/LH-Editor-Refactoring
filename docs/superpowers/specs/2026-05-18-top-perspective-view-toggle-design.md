# Top / 3D Perspective View Toggle Design

## Goal

After finishing edits in 2D Top View, the user can switch from the top toolbar to a 3D Perspective view and inspect the same editable scene without exporting, rebuilding, or duplicating scene data.

## Selected Approach

Use a single Unity scene with two view modes:

- `Top`: existing 2D orthographic editing view.
- `Perspective3D`: existing 3D camera navigation view for inspection.

The scene's wall, room, opening, and furniture objects remain the source of truth. The view toggle only changes which camera, input handler, and overlay UI are active.

## Architecture

Add a focused `EditorViewModeManager` that owns only view state. It should not replace or extend `ModeManager`, because `ModeManager` already represents editing modes such as `Default`, `RoomCreate`, `DetailEdit`, `DoorInsert`, `WindowInsert`, and `FurniturePlace`.

`EditorViewModeManager` will:

- Store the current `EditorViewMode`.
- Bind optional toolbar buttons for Top and 3D Perspective.
- Activate the Top camera and `CameraManager` in `Top`.
- Activate the Perspective camera and `CameraManager_3D` in `Perspective3D`.
- Hide Top View-only UI roots in `Perspective3D`.
- Restore Top View-only UI roots in `Top`.
- Raise a `ViewModeChanged` event for future UI or status surfaces.

This keeps edit mode and view mode as independent axes:

- Edit mode answers: what operation is active?
- View mode answers: how is the scene being viewed?

## Components

### `EditorViewMode`

A small enum:

```csharp
public enum EditorViewMode
{
    Top = 0,
    Perspective3D = 1,
}
```

### `EditorViewModeManager`

A scene component with serialized references:

- `Camera topCamera`
- `Camera perspectiveCamera`
- `CameraManager topCameraManager`
- `CameraManager_3D perspectiveCameraManager`
- `GameObject[] topViewOnlyRoots`
- `Button topButton`
- `Button perspectiveButton`
- `EditorViewMode initialViewMode`

It exposes:

- `EditorViewMode CurrentViewMode`
- `event Action<EditorViewMode> ViewModeChanged`
- `void SetViewMode(EditorViewMode mode)`
- `void SetTopView()`
- `void SetPerspectiveView()`
- `void SetReferencesForTests(...)`

## Data Flow

1. User edits walls, rooms, openings, or furniture in Top View.
2. Existing managers update the same runtime scene objects.
3. User clicks the `3D` toolbar toggle.
4. `EditorViewModeManager` enables the Perspective camera and `CameraManager_3D`, disables the Top camera and `CameraManager`, and hides top-plan overlays.
5. User navigates with existing `CameraManager_3D` pan, rotate, and zoom behavior.
6. User clicks the `Top` toolbar toggle.
7. `EditorViewModeManager` restores the Top camera, Top input, and top-plan overlays.

No scene data conversion occurs during the switch.

## UI Behavior

The initial toolbar can be minimal:

- `Top` button
- `3D` or `Perspective` button

The active view's button is non-interactable, matching the existing `ModeManager` button behavior. The inactive view button remains interactable.

The 3D view is inspection-only for the first implementation. Existing edit-mode state is not cleared, but Top View-only editing overlays are hidden while Perspective view is active.

## Error Handling

Missing references should not crash play mode:

- If a camera is missing, log a warning and skip toggling that camera.
- If a camera manager is missing, log a warning and skip toggling that component.
- If a top-view-only root is null, ignore it.
- Button binding should tolerate unassigned buttons.

The manager should still update `CurrentViewMode` when references are incomplete so tests and partially wired scenes behave predictably.

## Testing

Add focused EditMode tests for:

- Switching to `Perspective3D` enables Perspective camera and 3D camera manager while disabling Top camera and Top camera manager.
- Switching back to `Top` restores the inverse state.
- Top View-only roots are hidden in `Perspective3D` and restored in `Top`.
- Button interactability mirrors active view state.
- Setting the same view twice does not fire duplicate `ViewModeChanged` events.

## Manual Scene Wiring

Attach `EditorViewModeManager` to a scene object and assign:

- Existing Top camera.
- Existing Perspective camera.
- Existing `CameraManager`.
- Existing `CameraManager_3D`.
- Top View-only UI roots such as TopPlan content and handle canvases.
- Toolbar buttons for `Top` and `3D`.

If practical during implementation, add conservative automatic discovery using scene names and existing `LayerUtility` helpers. Manual serialized references remain the primary wiring path.

## Out Of Scope

- Split-screen 2D/3D preview.
- Separate preview scene generation.
- Editing walls, rooms, openings, or furniture directly in 3D view.
- Changing save/load schema.
- Rebuilding wall, room, or furniture geometry during view switches.
