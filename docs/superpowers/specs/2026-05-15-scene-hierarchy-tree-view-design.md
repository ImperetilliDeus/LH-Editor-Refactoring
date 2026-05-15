# Scene Hierarchy Tree View Design

## Goal

Add a standalone UGUI tree view that shows the current scene's Room and Wall hierarchy in the LH Editor, similar to Unity's Hierarchy window.

The first implementation focuses on visibility and low-risk selection sync:

- Rooms appear as parent rows.
- Walls assigned to a room appear as indented child rows under that room.
- Walls that are not assigned to any room appear at the root level.
- Clicking a wall row selects the matching wall in the 3D scene through `WallSelectionManager.SetSelectedWall`.
- Room row selection is out of scope for this pass.

## Chosen UI Direction

Use a dedicated hierarchy panel rather than extending the existing Room edit panel.

This keeps the scene hierarchy global and avoids coupling it to the selected-room workflow in `RoomAuthoringPanelManager` and `RoomWallAuthoringPanelController`. The component can be dropped onto an existing Canvas and wired to a `ScrollRect` content root.

## Existing System Context

Room data is owned by `RoomManager` and `Room`.

- `RoomManager.GetAllRooms()` returns the active rooms.
- `RoomManager.RoomsChanged` signals room creation, deletion, metadata changes, and wall assignment changes.
- `Room.EffectiveWallIds` returns the wall IDs currently associated with the room, respecting manual wall selection.
- `Room.RoomName` is the user-facing name. Empty names should fall back to the room GameObject name.

Wall data is owned by `Wall` and `WallData`.

- `WallHierarchyUtility.CollectWalls(wallRoot, results, true)` collects wall components under the scene wall root.
- `Wall.Data.id` is the stable relationship key used by rooms.
- `WallSelectionManager.SetSelectedWall(GameObject wall)` is the low-risk public API for selecting a wall from the hierarchy.
- Walls inside `WallOpeningContainer` should be represented by their logical export root, not as duplicate generated segments.

## Architecture

Add one new runtime component:

`SceneHierarchyTreeView`

Responsibilities:

- Resolve scene references when serialized references are missing:
  - `RoomManager`
  - `WallSelectionManager`
  - `wallRoot` by `LayerUtility.DefaultWallRootName`
  - scroll content root from a serialized `RectTransform`
- Subscribe to:
  - `RoomManager.RoomsChanged`
  - `WallRegistry.RegistryChanged`
- Rebuild the tree when rooms or walls change.
- Create UGUI row objects under the scroll content root.
- Maintain a lightweight row model so wall rows know which representative `Wall` they select.

No changes are required to `RoomManager`, `Room`, or `WallSelectionManager` for the first pass.

## Tree Building Rules

1. Collect all walls under `wallRoot`, including inactive walls.
2. Collapse generated opening segments by grouping walls by logical wall root:
   - if a wall is inside `WallOpeningContainer`, use the container transform as the logical root;
   - otherwise use the wall transform.
3. For each logical wall item, choose a representative selectable wall:
   - prefer an active wall;
   - ignore hidden opening base segments where possible;
   - fall back to the first available wall.
4. Build a lookup from wall ID to logical wall item.
5. Iterate rooms from `RoomManager.GetAllRooms()`.
6. For each room:
   - render a parent room row;
   - for each ID in `room.EffectiveWallIds`, render the matching logical wall as an indented child;
   - mark that logical wall item as assigned.
7. After rooms are rendered, render unassigned logical wall items at root level.
8. Sort rooms and root walls by display name for stable UI ordering.

If the same wall ID is present in multiple rooms, render it under each room because the current data model permits shared wall references.

## Display Names

Room row display:

- If `Room.RoomName` is not empty, show `Room (<RoomName>)`.
- Otherwise show the room GameObject name.
- If both are unavailable, show `Room <index>`.

Wall row display:

- Use the logical wall root transform name when available.
- Otherwise use the wall GameObject name.
- Otherwise show `Wall <index>`.

## UI Behavior

The component uses UGUI because the project already uses UGUI controls for related panels.

Minimum row behavior:

- Room rows are non-selecting parent rows.
- Wall rows are `Button` rows.
- Wall rows are indented with a configurable child indent.
- The row template can be provided in the inspector.
- If no row template is assigned, the component creates a basic `Button + Text` row at runtime so the feature remains usable in a scene before prefab polish.

Optional fold/unfold can be added later. The first implementation keeps all rows expanded to keep data flow simple and predictable.

## Selection Sync

Only Wall click sync is included.

When a wall row is clicked:

1. Resolve the representative `Wall`.
2. If `WallSelectionManager` exists, call `SetSelectedWall(representativeWall.gameObject)`.
3. If no selection manager is available, do nothing except keep the UI intact.

Room row selection is intentionally excluded because current room selection is managed through private methods in `RoomAuthoringPanelManager`. Adding public room selection should be a separate design decision.

## Error Handling

- Missing `wallRoot`: show an empty tree and log a warning once.
- Missing scroll content root: do not build rows and log a warning once.
- Null rooms, destroyed walls, empty IDs, and missing wall IDs are skipped.
- Missing `WallSelectionManager`: wall rows remain visible but clicks do not affect scene selection.

## Testing

Add focused EditMode tests for the tree-building behavior where possible:

- standalone walls are rendered at root level;
- room-assigned walls are rendered as children under the room row;
- assigned walls do not also render as root standalone walls;
- wall row click calls into the selection path when a `WallSelectionManager` reference is provided.

If direct UGUI click testing is too brittle, split row model generation into an internal helper that can be tested without Canvas event plumbing, while keeping rendering in `SceneHierarchyTreeView`.

## Out of Scope

- Room row selection sync.
- Fold/unfold persistence.
- Drag reordering.
- Renaming rooms or walls from the tree.
- Context menus.
- Search/filter.
- UI Toolkit implementation.
