# Work State Save/Load

`LhWorkStatePersistenceController` saves the editable LH Editor work state as a JSON-backed `.lhscene` file and loads it by replacing the current editable state.

## Saved State

- Walls and wall editor flags
- Door/window opening placement values
- Rooms, room metadata, room surfaces, and wall references
- Placed furniture resolved by `FurnitureCatalog` item code, export code, or native code

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
