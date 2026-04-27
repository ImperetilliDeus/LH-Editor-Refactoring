# Mobile Viewer Schema v2

## Purpose

This document defines a recommended JSON schema for the LH mobile viewer.

The goal is not only to rebuild 3D space from editor data, but also to support:

- room-aware selection
- element-type-aware defect flows
- stable native/app handoff codes
- future mobile viewer refactoring without breaking editor internals

The design keeps the useful legacy shape:

- top-level `startPoint`
- top-level `wallData`
- top-level `roomData`

It adds business metadata needed for defect registration.

## Design Principles

### 1. Separate rendering data from business data

The mobile viewer needs two different concerns:

- geometry to reconstruct the apartment in 3D
- business identifiers to decide what the user selected and what code to send to the native app

These concerns should stay in the same JSON package, but they should not be mixed implicitly.

### 2. Keep room ownership explicit

A room must explicitly declare which wall, floor, ceiling, window, door, and furniture elements belong to it.

This is required because the viewer does more than render:

- it shows room-specific defect types
- it must build a path like `거실 > 벽 > 갈라짐`
- it must send stable room and element codes to the native app

### 3. Make selectable targets first-class objects

The viewer does not operate on meshes directly. It operates on selectable business targets such as:

- living room north wall
- living room floor
- bedroom window 1
- bathroom ceiling

Those targets need stable ids and codes even if mesh generation changes later.

### 4. Preserve editor freedom

The editor should keep its internal room/wall/furniture model clean.
The mobile viewer schema should be produced by an export adapter, not become the editor's internal data model.

## Findings From Mobile-Viewer-Old

The old mobile viewer does not simply render exported geometry.
Its runtime contract is centered on three business values:

- `mntnSpceCd`: room or maintenance space code
- `locCd`: location or element code
- `reonMtrlCd`: selected defect/material code

Observed runtime flow in the old viewer:

- `Communicator` receives room JSON from React Native and passes it to `RoomManager`.
- `RoomManager` reconstructs geometry from `startPoint`, `wallData`, and `roomData`.
- `InterestManager` resolves the touched target into `mntnSpceCd` and `locCd`.
- `DefectManager` filters valid defect choices through `TypeManager.GetWebMtrls(mntnSpceCd, locCd)`.
- The final payload returned to the native app contains the selected `mntnSpceCd`, `locCd`, `reonMtrlCd`, and hit/user transforms.

This means the JSON is not just a scene description.
It is the source of truth for defect-selection context.

## Legacy Compatibility Requirements

If the editor must continue to support the old mobile viewer before that app is refactored, the exported JSON must still preserve these legacy assumptions:

- top-level `startPoint`, `wallData`, and `roomData`
- each room has a `code` field that the viewer uses as `mntnSpceCd`
- each room explicitly owns its `walls`, `floor`, and `ceil`
- `furnish[*].defects[*]` carries explicit tuples of:
  - `mntnCd`
  - `locCd`
  - `mtrlCd`
- floor and ceiling objects receive room context after import
- wall segments expose a location `type` that the viewer uses as `locCd`

Without these fields, the old viewer cannot keep its current selection and submission flow.

## Recommended Top-Level Shape

```json
{
  "version": 2,
  "unitTypeCode": "55A",
  "startPoint": { "x": 0, "y": 0, "z": 0 },
  "wallData": [],
  "roomData": [],
  "elements": [],
  "defectCatalog": [],
  "exportMeta": {
    "coordinateSystem": "unity-left-handed",
    "unit": "cm",
    "source": "LH Editor Refactoring"
  }
}
```

For transition, the recommended strategy is:

- keep the legacy fields required by `Mobile-Viewer-Old`
- add new v2 business fields beside them
- refactor the mobile viewer later to consume the new business layer directly

## Core Structures

### `wallData`

`wallData` remains the geometry reconstruction source for walls.

Recommended shape:

```json
{
  "name": "Wall24",
  "id": 24,
  "position": { "x": 33.5, "y": 11.0, "z": 2.0 },
  "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "scale": { "x": 18.5, "y": 22.0, "z": 1.5 },
  "segments": [
    {
      "position": { "x": -0.29, "y": 0.0, "z": 0.0 },
      "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "scale": { "x": 0.42, "y": 1.0, "z": 1.0 },
      "hasInterior": false,
      "door": null,
      "window": null
    }
  ]
}
```

Rules:

- `wallData` is for reconstruction, not for defect submission semantics.
- `id` must be stable within the exported document.
- `segments` must be ordered consistently along the wall span.
- wall scale/position must be raw exported geometry, not viewer-specific adjusted values.

### `roomData`

`roomData` remains the room reconstruction source, but gains explicit room ownership metadata.

Recommended shape:

```json
{
  "id": "room_living",
  "name": "거실",
  "code": "900",
  "roomTypeKey": "LIVING",
  "nativeCode": "RM001",
  "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "scale": { "x": 1.0, "y": 1.0, "z": 1.0 },
  "walls": [24, 25, 27, 28],
  "floor": {
    "id": "floor_room_living",
    "position": { "x": 0.0, "y": 0.01, "z": 0.0 },
    "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale": { "x": 18.0, "y": 1.0, "z": 22.0 },
    "meshType": 1,
    "mesh": {},
    "texture": "F002"
  },
  "ceil": {
    "id": "ceil_room_living",
    "position": { "x": 0.0, "y": 22.0, "z": 0.0 },
    "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
    "scale": { "x": 18.0, "y": 1.0, "z": 22.0 },
    "meshType": 1,
    "mesh": {},
    "texture": "C001"
  },
  "furnish": [],
  "elementIds": [
    "elem_room_living_wall_24",
    "elem_room_living_wall_25",
    "elem_room_living_floor",
    "elem_room_living_ceil",
    "elem_room_living_window_1"
  ]
}
```

Rules:

- `id` must be document-stable and independent from Unity object names.
- `code` is the editor/business room code.
- `nativeCode` is the room code passed to the React Native host app.
- `walls` defines the wall geometry roots that belong to the room.
- `floor` and `ceil` are always explicit room-owned surfaces.
- `elementIds` defines all selectable business targets under the room.
- `code` must continue to map to the legacy viewer's `mntnSpceCd`.

### `elements`

`elements` is the new business layer.
This is the most important addition.

Each entry represents a selectable target in the viewer.

```json
{
  "id": "elem_room_living_wall_24",
  "roomId": "room_living",
  "type": "WALL",
  "subtype": "MAIN",
  "name": "거실 벽 1",
  "nativeCode": "EL_WALL_001",
  "meshRef": {
    "kind": "wall",
    "wallId": 24,
    "segmentIndex": null
  },
  "defectGroupIds": ["wall_finish", "wall_crack"],
  "metadata": {
    "textureCode": "W001"
  }
}
```

Possible `type` values:

- `WALL`
- `FLOOR`
- `CEIL`
- `DOOR`
- `WINDOW`
- `FURNITURE`
- `FIXTURE`

Why `elements` is necessary:

- geometry ids and business ids should not be the same concern
- one wall mesh may need to become multiple selectable targets later
- the allowed defect list is attached more naturally to a business element than to raw wall geometry

### Legacy Mapping Notes

The old mobile viewer currently derives business meaning in brittle ways:

- room ownership for furniture is resolved by hit position and room lookup
- wall and surface selection depends on `WallSegment.type` for `locCd`
- some wall hits use nearest-room inference instead of explicit exported ownership

The new schema should remove these implicit rules over time by making business mapping explicit:

- `roomData.code` remains the legacy room-space code
- `elements[*].nativeCode` should become the future canonical `locCd`
- `elements[*].roomId` should replace nearest-room inference
- furniture should stop inferring location codes from runtime room lookups and instead carry explicit exported mappings

### `defectCatalog`

`defectCatalog` describes what can be submitted.

```json
{
  "id": "wall_crack",
  "name": "갈라짐",
  "nativeCodes": {
    "mntnCd": "SPCE001",
    "locCd": "LOC001",
    "mtrlCd": "MTRL001"
  },
  "appliesTo": {
    "roomTypeKeys": ["LIVING", "BEDROOM"],
    "elementTypes": ["WALL"]
  }
}
```

Rules:

- these are shared app handoff codes, not display-only labels
- the viewer may filter available defects by:
  - room type
  - element type
  - explicit `element.defectGroupIds`

## Why Room Ownership Must Be Explicit

Yes, the schema should explicitly define what walls, floor, and ceiling belong to each room.

Reasons:

- the mobile viewer needs room-based navigation and filtering
- defect lists differ by room and by element type
- the same geometry may be visually adjacent but operationally belong to different rooms
- native app payloads need unambiguous room context

Recommended policy:

- floor and ceiling belong to exactly one room
- wall geometry may be shared visually, but exported selectable elements should be room-scoped
- doors and windows should also be room-scoped selectable elements, even if their geometry sits on a shared wall

## Geometry vs Business Mapping

The schema should support this split:

- `wallData`: renderable wall geometry
- `roomData.floor` / `roomData.ceil`: renderable room surfaces
- `elements`: selectable business targets that point back to geometry

This allows future viewer changes such as:

- different hit-testing logic
- different highlighting logic
- grouping multiple geometry pieces into one selectable target
- keeping native handoff codes stable even if mesh generation changes

## Recommended Export Rules

### Walls

- export raw wall transforms without magic offsets
- export segments in deterministic order
- export door/window transforms relative to their wall root
- keep door/window code values explicit
- define how each wall-selectable surface maps to legacy `locCd`

### Rooms

- export room id, code, type, and native code
- export room-owned wall ids
- export floor and ceiling ids
- export floor/ceiling scale even when meshType is default
- export room surfaces from normalized room geometry rather than inferred scene-only transforms
- keep `code` aligned with the maintenance-space code used by the mobile app

### Furniture

- export stable furniture id and code
- export owning room id
- for legacy compatibility, keep explicit furnish defect tuples
- in v2, also export furniture as a selectable business element

### Defect Codes

- do not hardcode defect options in the mobile viewer
- export or join them as stable ids and native codes
- support room + element filtering

## Legacy-to-V2 Mapping Table

| Legacy viewer concept | Current old viewer source | Recommended v2 source |
| --- | --- | --- |
| Space code | `roomData[*].code` | `roomData[*].code` |
| Location code for wall/floor/ceil | `WallSegment.type` | `elements[*].nativeCode` |
| Location code for furniture | `furnish.defects[*].locCd` via runtime lookup | `elements[*].nativeCode` plus optional compatibility tuples |
| Valid defect list | `TypeManager.GetWebMtrls(space, loc)` | `defectCatalog` and/or external master data |
| Touched target ownership | runtime hit inference | explicit `elements[*].roomId` and `meshRef` |

## Recommended Example

```json
{
  "version": 2,
  "unitTypeCode": "55A",
  "startPoint": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "wallData": [],
  "roomData": [
    {
      "id": "room_living",
      "name": "거실",
      "code": "900",
      "roomTypeKey": "LIVING",
      "nativeCode": "RM001",
      "position": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
      "scale": { "x": 1.0, "y": 1.0, "z": 1.0 },
      "walls": [24, 25, 27, 28],
      "floor": {
        "id": "floor_room_living",
        "position": { "x": 0.0, "y": 0.01, "z": 0.0 },
        "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
        "scale": { "x": 18.0, "y": 1.0, "z": 22.0 },
        "meshType": 1,
        "mesh": { "vertices": [], "triangles": [], "normals": [], "uvs": [] },
        "texture": "F002"
      },
      "ceil": {
        "id": "ceil_room_living",
        "position": { "x": 0.0, "y": 22.0, "z": 0.0 },
        "angle": { "x": 0.0, "y": 0.0, "z": 0.0 },
        "scale": { "x": 18.0, "y": 1.0, "z": 22.0 },
        "meshType": 1,
        "mesh": { "vertices": [], "triangles": [], "normals": [], "uvs": [] },
        "texture": "C001"
      },
      "furnish": [],
      "elementIds": [
        "elem_room_living_wall_24",
        "elem_room_living_floor",
        "elem_room_living_ceil"
      ]
    }
  ],
  "elements": [
    {
      "id": "elem_room_living_wall_24",
      "roomId": "room_living",
      "type": "WALL",
      "subtype": "MAIN",
      "name": "거실 벽 1",
      "nativeCode": "EL_WALL_001",
      "meshRef": { "kind": "wall", "wallId": 24, "segmentIndex": null },
      "defectGroupIds": ["wall_crack", "wall_finish"],
      "metadata": {}
    },
    {
      "id": "elem_room_living_floor",
      "roomId": "room_living",
      "type": "FLOOR",
      "subtype": "ROOM_FLOOR",
      "name": "거실 바닥",
      "nativeCode": "EL_FLOOR_001",
      "meshRef": { "kind": "room-floor", "roomId": "room_living" },
      "defectGroupIds": ["floor_scratch", "floor_lift"],
      "metadata": {}
    }
  ],
  "defectCatalog": [
    {
      "id": "wall_crack",
      "name": "갈라짐",
      "nativeCodes": {
        "mntnCd": "SPCE001",
        "locCd": "LOC001",
        "mtrlCd": "MTRL001"
      },
      "appliesTo": {
        "roomTypeKeys": ["LIVING", "BEDROOM"],
        "elementTypes": ["WALL"]
      }
    }
  ],
  "exportMeta": {
    "coordinateSystem": "unity-left-handed",
    "unit": "cm",
    "source": "LH Editor Refactoring"
  }
}
```

## Required Implementation Phases

### Phase 1. Match the legacy mobile viewer contract

Goal:

- make the current editor export safely consumable by `Mobile-Viewer-Old`

Required work:

- emit exact legacy shape for `startPoint`, `wallData`, and `roomData`
- keep direct `position`, `angle`, and `scale` fields instead of nested `transform`
- export room `code`, owned wall ids, floor, ceil, and furnish arrays
- export furnish `defects` tuples for old furniture selection flow
- ensure wall segment ordering is deterministic
- ensure floor and ceiling scale values match what the viewer reconstructs
- verify imported data still drives `mntnSpceCd` and `locCd` correctly in the old viewer

### Phase 2. Add missing metadata authoring in the editor

Goal:

- allow authors to define all codes the viewer and native host app depend on

Required work:

- room id / room code / room type key / room native code
- floor texture code / ceiling texture code
- wall texture or finish code where needed
- door/window export code mapping
- furniture export code mapping
- furniture defect tuple authoring where legacy compatibility is required
- validation for missing room-space codes and missing location codes

### Phase 3. Add explicit business elements in parallel with legacy data

Goal:

- prepare the future viewer refactor without breaking the current viewer

Required work:

- define element ids
- map each element to room ownership
- map each element to geometry references
- define native handoff codes per element
- keep legacy `roomData` fields in place during the transition

### Phase 4. Add defect catalog integration

Goal:

- reduce hardcoded filtering logic in the mobile viewer

Required work:

- import or author defect master data
- map defect groups to room types and element types
- export defect ids and native codes
- define how external master data and exported compatibility tuples coexist

### Phase 5. Refactor the mobile viewer to consume v2 business data

Goal:

- remove fragile runtime inference from the viewer

Required work:

- replace nearest-room lookup with `elements[*].roomId`
- replace `WallSegment.type`-driven location semantics with explicit element metadata
- replace furniture room lookup hacks with direct element payloads
- keep RN payload contract stable while changing internal lookup logic

### Phase 6. Add export validation and regression fixtures

Goal:

- prevent silent breakage in both the editor exporter and viewer importer

Required work:

- detect missing room ids or native codes
- detect missing element mappings
- detect rooms without valid walls/floor/ceil
- detect door/window/furniture codes missing from export
- detect invalid defect references
- golden sample JSON export
- schema regression tests
- optional viewer-side import smoke test fixtures

## Concrete Feature List for This Editor

The editor will eventually need these features:

1. Stable export ids for rooms, elements, and room-owned surfaces.
2. Room metadata editing for `roomTypeKey`, `code`, and `nativeCode`.
3. Surface metadata editing for floor and ceiling texture/native mappings.
4. Opening metadata editing for exportable door/window codes.
5. Furniture metadata editing for export/native codes and legacy defect tuples.
6. Legacy-compatible schema adapter that writes old-viewer JSON without polluting internal editor classes.
7. Element generation logic that creates room-scoped selectable targets.
8. Defect catalog mapping from room type + element type to shared app codes.
9. Export validation UI that reports missing business metadata.
10. Compatibility fixtures that prove one export works in both legacy and refactored viewers.

## Recommended Build Order

1. Match the old mobile viewer's JSON contract exactly.
2. Add room ids and room-native metadata.
3. Add room-owned floor/ceil ids and deterministic wall references.
4. Preserve legacy furnish defect tuples while introducing `elements`.
5. Introduce `defectCatalog`.
6. Refactor the mobile viewer to read `elements`.
7. Add validator and regression fixtures.

This order keeps the current exporter usable while extending it toward the mobile viewer workflow.
