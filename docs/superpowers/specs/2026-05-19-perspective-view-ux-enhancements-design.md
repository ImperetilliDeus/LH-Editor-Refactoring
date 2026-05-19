# Perspective View UX Enhancements Design

## Goal

Extend the existing Top / 3D Perspective toggle so the 3D view is easier to understand and verify:

1. Improve Top / 3D button icons and active-state feedback.
2. Fit the 3D camera to the whole editable space when entering Perspective view.
3. Move the 3D camera to the selected room or wall when a selection exists.
4. Show read-only selection highlight in 3D without enabling 3D editing.

These enhancements build on the existing `EditorViewModeManager` and keep the same source-of-truth scene objects.

## Design Principles

- Preserve the current edit model: Top View remains the editing surface, and 3D remains inspection-only.
- Do not duplicate wall, room, opening, or furniture data.
- Prefer small services that can be tested without a fully wired scene.
- Reuse existing selection sources instead of creating a parallel selection model.
- Make scene wiring explicit, but tolerate missing optional references with warnings and graceful fallbacks.

## Existing Context

The current feature branch already adds:

- `EditorViewMode`
- `EditorViewModeManager`
- camera and camera-manager toggling
- Top View-only root visibility toggling
- toolbar button binding

Existing selection APIs available for the extension:

- `WallSelectionManager.SelectedWall`
- `WallSelectionManager.SelectionChanged`
- `WallSelectionManager.SelectionSetChanged`
- `WallSelectionManager.GetSelectedWalls(List<GameObject>)`
- `RoomAuthoringPanelManager.SelectedRoom`
- `RoomAuthoringPanelManager.SelectedRoomChanged`
- `Room.Centroid`
- `Room.BoundaryVertices`
- `Room.SetSelectionState(bool, Color)`

## Enhancement 1: Button Icons And State Feedback

Add a small presenter component or focused helper inside the view mode feature that updates toolbar visuals whenever the view changes.

Recommended component:

```csharp
public sealed class EditorViewModeToolbarPresenter : MonoBehaviour
```

Responsibilities:

- Bind to `EditorViewModeManager.ViewModeChanged`.
- Update optional label text such as `Top` and `3D`.
- Update optional `Image` icon slots for Top and 3D buttons.
- Apply active/inactive colors to button images or backgrounds.
- Keep the currently active view visually obvious even if the button is non-interactable.

The presenter should use serialized references:

- `EditorViewModeManager viewModeManager`
- `Button topButton`
- `Button perspectiveButton`
- optional `Image topIcon`
- optional `Image perspectiveIcon`
- optional `Text` or `TMP_Text` active status label
- active/inactive colors

If the current UI already uses sprite-only buttons, icon sprites can be assigned in the Inspector. The code should not require new art assets to compile; missing sprites should simply leave the image unchanged.

## Enhancement 2: Fit 3D Camera To Whole Editable Space

Add a camera framing service that computes a world-space bounds around the editable scene and places the Perspective camera so the content is visible.

Recommended component:

```csharp
public sealed class PerspectiveCameraFramingController : MonoBehaviour
```

Responsibilities:

- Listen for `EditorViewModeManager.ViewModeChanged`.
- When the new mode is `Perspective3D`, compute target bounds and frame them.
- Prefer selected room/wall bounds if configured to focus selection first.
- Fall back to whole-scene bounds.
- If no bounds are available, keep the current camera transform and log a warning once.

Bounds sources, in priority order for whole-scene fit:

1. `wallRoot` renderers/colliders.
2. `roomRoot` or `RoomManager.GetAllRooms()` room boundaries.
3. `furnitureRoot` renderers/colliders.
4. `Grid` renderer/collider as fallback.

Camera placement should be deterministic:

- Look at the bounds center.
- Use a configurable yaw and pitch, for example yaw `-35`, pitch `45`.
- Use camera field of view and bounds extents to compute a distance.
- Clamp distance between `minDistance` and `maxDistance`.
- Apply transform directly to the Perspective camera.

This should be a framing jump, not a smooth animation, for the first implementation.

## Enhancement 3: Focus Selected Room Or Wall

When entering 3D, if a room or wall is selected, frame that selected object instead of the whole scene.

Selection priority:

1. Selected room from `RoomAuthoringPanelManager.SelectedRoom`.
2. Primary selected wall from `WallSelectionManager.SelectedWall`.
3. Multiple selected walls from `WallSelectionManager.GetSelectedWalls(...)`.
4. Whole-scene fit.

The selection focus should not change selection state. It only reads existing state.

Bounds computation:

- For a selected room, use `Room.BoundaryVertices` when available, with a configurable vertical height fallback.
- For a selected wall GameObject, use child renderers/colliders first, then the `Wall.Data` start/end/thickness/height if renderer bounds are unavailable.
- For multiple selected walls, encapsulate all selected wall bounds.

Expose a public method:

```csharp
public bool FocusCurrentSelectionOrScene()
```

This lets the toolbar or future shortcut call the same behavior explicitly.

## Enhancement 4: Read-Only 3D Selection Highlight

Add a 3D highlight controller that mirrors existing Top View selection while Perspective view is active.

Recommended component:

```csharp
public sealed class PerspectiveSelectionHighlightController : MonoBehaviour
```

Responsibilities:

- Listen to `EditorViewModeManager.ViewModeChanged`.
- Listen to `WallSelectionManager.SelectionChanged` and `SelectionSetChanged`.
- Listen to `RoomAuthoringPanelManager.SelectedRoomChanged`.
- Apply read-only highlight when Perspective view is active.
- Clear highlight when returning to Top view or when selection is cleared.

Implementation should avoid permanently changing authored materials.

Recommended first implementation:

- Add temporary child highlight objects or overlay components rather than replacing shared materials.
- For walls, use a thin transparent overlay mesh or child bounding outline.
- For rooms, prefer existing `Room.SetSelectionState(...)` only if it does not conflict with Top View selection. If it does conflict, use a separate transient room highlight child.
- Destroy or disable transient highlight objects on selection changes.

The highlight must not enable dragging, editing, deletion, or property mutation in 3D.

## Data Flow

1. User edits in Top View.
2. User selects a room or wall, optionally.
3. User clicks `3D`.
4. `EditorViewModeManager` switches cameras and input handlers.
5. Toolbar presenter updates visual state.
6. Framing controller computes selected bounds or whole-scene bounds and positions the Perspective camera.
7. Highlight controller mirrors current selection in 3D.
8. User inspects the scene using 3D navigation.
9. User clicks `Top`.
10. Highlight controller clears transient 3D highlight, Top View-only roots are restored, and edit state remains unchanged.

## Error Handling

- Missing presenter references should not block view switching.
- Missing framing roots should fall back through available roots.
- No available bounds should log one warning and keep the camera unchanged.
- Missing selection managers should simply disable selection focus/highlight features.
- Highlight cleanup must tolerate destroyed or missing selected objects.

## Testing

Add EditMode tests for pure logic and component behavior where possible:

- Toolbar presenter updates active/inactive visual state when view mode changes.
- Camera framing computes a valid camera pose for a known bounds.
- Selection focus uses selected room before selected wall.
- Whole-scene fit is used when no selection exists.
- Highlight controller creates highlight only in `Perspective3D`.
- Highlight controller clears highlight on return to `Top`.
- Camera managers still ignore input when disabled.

Because existing editor tests use reflection to access `Assembly-CSharp` types, tests may continue that pattern unless assembly definitions are changed deliberately.

## Manual QA

Manual checks in Unity Editor:

- Top / 3D buttons clearly show the active view.
- Entering 3D after drawing walls frames the whole plan.
- Entering 3D with a selected room frames that room.
- Entering 3D with a selected wall frames that wall.
- 3D highlight appears for the selected room/wall.
- 3D highlight disappears when returning to Top.
- 3D navigation still works after automatic framing.
- No wall, room, opening, furniture, material, save/load, or export data is changed by view switching.

## Out Of Scope

- Split-screen preview.
- Animated camera transitions.
- 3D direct editing.
- New save/load schema.
- New room or wall selection model.
- Runtime mobile viewer changes.
