# LH Viewer v1 JSON Contract

This document fixes the legacy mobile viewer JSON contract used by `LHM_260212`.
The editor export mode `LhSceneExportBuilder.ExportMode.LegacyExact` writes this
contract, and the viewer reads the same shape through `JsonUtility.FromJson<RoomData>()`.

## Scope

Viewer v1 JSON is a runtime reconstruction contract, not an editor work-state
contract. It should contain only the data needed for the viewer to rebuild the
space and support viewer workflows such as room lookup, minimap framing, object
selection, and defect context.

Editor-only state such as wall handles, split-point flags, manual selection UI
state, undo metadata, and editor refresh state stays in `.lhscene` work-state
files.

## Root

The root object must contain:

- `startPoint`: viewer character start position.
- `wallData`: non-empty list of walls.
- `roomData`: non-empty list of rooms.

The legacy viewer v1 JSON must not require a `version` field. The editor may
have newer internal schemas, but `LegacyExact` output preserves the unversioned
viewer v1 shape.

## Wall Data

Each `wallData[]` item must contain:

- `name`: wall object name.
- `id`: positive integer used by `roomData[].walls`.
- `texture`: wall material code. Empty values are interpreted by the viewer as
  its default material.
- `position`, `angle`, `scale`: wall root transform in the legacy viewer
  coordinate semantics.
- `segments`: non-empty list of wall segments.

Each segment must contain:

- `position`, `angle`, `scale`: local segment transform.
- `hasInterior`: true when the segment carries a door or window.
- `door`: present when a door exists.
- `window`: present when a window exists.

If `door.isExist` or `window.isExist` is true, its `code`, `position`, `angle`,
and `scale` fields are required. Parametric openings may also include
`parametricProfileKey`, `authoredSize`, `width`, `height`, `depth`, and
`bottomY`.

## Room Data

Each `roomData[]` item must contain:

- `name`: viewer room name.
- `code`: mobile room code. This is required by viewer defect context.
- `position`, `angle`, `scale`: room transform.
- `walls`: non-empty list of wall ids. Every id must exist in `wallData`.
- `floor`: floor surface.
- `ceil`: ceiling surface.
- `furnish`: list of furniture. Use an empty list when the room has none.

Surface data must contain `position`, `angle`, `scale`, `meshType`, `mesh`, and
`texture`. If `meshType` is non-zero, `mesh.vertices`, `mesh.triangles`,
`mesh.normals`, and `mesh.uvs` must be present and internally consistent.

## Furniture And Defects

Each furniture item must contain:

- `code`: viewer prefab/material lookup code.
- `position`, `angle`, `scale`: transform relative to the room.
- `defects`: list of defect tuples. Use an empty list when there are no defects.

Each defect tuple must contain:

- `mntnCd`
- `locCd`
- `mtrlCd`

## Compatibility Rules

- DTO classes may have different C# names in the editor and viewer. The stable
  contract is the JSON field shape.
- `JsonUtility` ignores unknown fields and silently defaults missing fields, so
  export validation must catch required-field and geometry problems before the
  JSON reaches the viewer.
- Layer names, not layer indices, should be used in tests. The editor and viewer
  can assign different numeric indices to the same named layer.
