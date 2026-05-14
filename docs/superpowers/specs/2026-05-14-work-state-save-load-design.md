# Work State Save/Load Design

## Goal

Add a project work-state save/load feature for LH Editor. The feature stores the current editable scene state to a dedicated JSON file and restores it later by replacing the current work state.

The saved state must cover walls, door/window openings, rooms, and placed furniture so users can resume editing from the same layout and metadata.

## Decisions

- Use a dedicated work-state JSON format, separate from the existing export JSON schema.
- Use schema versioning from the first release.
- On load, replace the current editable work state instead of merging with the active scene.
- Keep fixed scene infrastructure such as cameras, UI, managers, catalogs, and configured roots.
- Restore runtime objects through existing editor creation paths where possible.

## File Format

The root DTO is a versioned JSON document:

```json
{
  "version": 1,
  "walls": [],
  "rooms": [],
  "furniture": []
}
```

The default extension can remain `.json`. A future UI can use a custom extension such as `.lhscene` while keeping the file contents as JSON.

## Saved Data

### Walls

Each wall record stores the editable wall geometry and editor metadata:

- Stable wall id
- Object name
- Start and end points
- Thickness, height, and center Y
- Start/end vertex ids
- Start/end handle suppression flags
- Start/end split point flags
- Door/window openings attached to that wall span

Openings store:

- Opening type: door or window
- Door/window catalog key
- Door open direction and vertical flip values for doors
- Center distance along the wall
- Width, height, depth, and bottom Y

For walls with openings, the saved wall record represents the outer wall span. On restore, the loader rebuilds the opening container and generated wall segments.

### Rooms

Each room record stores:

- Room name
- Room type key
- Room code and native code
- Floor and ceiling texture codes
- Manual room flag
- Placement offset
- Boundary vertices
- Wall ids
- Manual wall selection enabled flag and manual wall ids

Rooms are restored after walls so their wall references can be resolved from saved ids.

### Furniture

Each furniture record stores:

- Catalog code
- Export code and native code for validation/debugging
- Object name
- World position, rotation, and local scale
- Placed flag
- Current room identity when it can be resolved

On restore, the loader resolves the prefab from `FurnitureCatalog` by catalog code and instantiates it under `FurnitureRoot`.

## Runtime Flow

### Save

1. Resolve required scene references: wall root, room manager, furniture root, and furniture catalog.
2. Collect active walls from the wall root.
3. Collapse opening containers into outer wall records and attach their openings.
4. Collect rooms from `RoomManager`.
5. Collect placed `FurnitureInstance` objects from `FurnitureRoot`.
6. Serialize the work-state DTO using Unity-compatible JSON.
7. Write the JSON to the selected path.

### Load

1. Read and parse the selected JSON file.
2. Validate the schema version and required collections.
3. Clear current editable work objects:
   - Walls and opening containers under the wall root
   - Rooms managed by `RoomManager`
   - Placed furniture under `FurnitureRoot`
4. Restore walls and opening containers.
5. Refresh wall handles and wall registry.
6. Restore rooms and reconnect wall ids.
7. Restore furniture through the catalog.
8. Refresh room topology, visual state, top-view rendering, and selection state.

## Error Handling

- Empty path: log a warning and skip.
- Unsupported version: log an error and do not clear the current scene.
- Malformed JSON: log an error and do not clear the current scene.
- Missing furniture catalog item: skip that furniture item and log a warning with its catalog code.
- Invalid wall geometry: skip that wall and log a warning.
- Missing wall reference in a room: restore the room polygon and keep the valid wall references.

The loader must validate the file before clearing the current work state.

## Components

Add a new namespace/folder under `Assets/Scripts/ProjectPersistence`:

- `LhWorkStateSchema.cs`: serializable DTOs for versioned work-state JSON.
- `LhWorkStateBuilder.cs`: reads current scene objects into DTOs.
- `LhWorkStateLoader.cs`: applies DTOs back into the scene.
- `LhWorkStatePersistenceController.cs`: MonoBehaviour entry point for save/load buttons and configured paths.

Small public restore helpers may be added to existing systems only where needed:

- `RoomManager`: clear all rooms and restore metadata/manual wall selection safely.
- `FurniturePlacementManager` or a dedicated helper: instantiate placed furniture from a catalog item without entering preview placement mode.
- `WallOpeningPlacementManager`: restore an opening container from saved opening data and rebuild generated segments.

## Tests

Use EditMode tests for DTO/build/load behavior where possible:

- Save builder captures a standalone wall with geometry and flags.
- Save builder captures a wall opening with type, dimensions, and catalog key.
- Loader replaces existing work state instead of merging.
- Loader restores room metadata and wall id references.
- Loader skips missing furniture catalog entries without aborting the whole load.

Manual Unity verification should cover:

- Create walls, doors/windows, rooms, and furniture.
- Save to JSON.
- Change or delete the scene state.
- Load the file.
- Confirm the restored layout, metadata, and furniture placement match the saved state.
