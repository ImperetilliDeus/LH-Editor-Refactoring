# Work State Save/Load

`LhWorkStatePersistenceController` saves the editable LH Editor work state as a JSON-backed `.lhscene` file and loads it by replacing the current editable state.

## Saved State

- Walls and wall editor flags
- Door/window opening placement values
- Rooms, room metadata, room surfaces, and wall references
- Placed furniture resolved by `FurnitureCatalog` item code, export code, or native code

## Furniture Room Assignment

Save, load and mobile export use one rule (`FurnitureRoomResolver`): the furniture keeps its current room while its footprint or position is still inside that room's polygon; otherwise it takes the first room in the room list that contains it. On load, the saved `roomName` is treated as the current room. The saved-data normalization (`LhWorkStateRoomUtility`) applies the same priority using the saved position only, because saved furniture has no bounds.

## Scene Wiring

Add `LhWorkStatePersistenceController` to a scene object and assign:

- `Wall Root`
- `Room Manager`
- `Furniture Root`
- `Furniture Catalog`
- Optional save/load buttons

The default path is `WorkStates/lh_work_state.lhscene` under the project root.

## Load Behavior

Loading validates the file before clearing current work. If the file has an unsupported version, invalid wall geometry, invalid room polygons, or unresolved furniture prefabs, the current scene state is left unchanged and the load returns a failed result.
