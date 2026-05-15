# Wall Mesh Defaults And Handle Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore wall MeshFilter/component defaults without bloating JSON and eliminate viewport-centered ghost handles.

**Architecture:** Keep work-state JSON concise by referencing runtime defaults from code. Ensure wall objects always receive a valid shared cube mesh through the wall factory/default provider path, and make handle registration/display reject invalid geometry or unresolved screen positions.

**Tech Stack:** Unity C#, existing wall factory, DrawManager defaults, HandleManager UI/registry.

---

### Task 1: Wall Mesh Runtime Defaults

**Files:**
- Modify: `Assets/Scripts/Draw/Wall/Core/WallObjectFactory.cs`
- Modify: `Assets/Scripts/Draw/Wall/Core/DrawManager.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`

- [ ] Add a default cube mesh fallback in `WallObjectFactory` so restored walls never keep a null MeshFilter.
- [ ] Expose DrawManager default wall mesh if needed for loader services.
- [ ] Use the same fallback for standalone and opening wall segments.

### Task 2: Handle Ghost Guard

**Files:**
- Modify: `Assets/Scripts/Draw/Wall/Core/HandleManager.Registry.cs`
- Modify: `Assets/Scripts/Draw/Wall/Core/HandleManager.UI.cs`
- Modify: `Assets/Scripts/Draw/Wall/Core/HandleManager.cs`

- [ ] Reject wall registration when endpoints are invalid or too short.
- [ ] Disable handles when screen conversion is invalid or off camera.
- [ ] Prune empty/invalid groups after hierarchy rebuild.

### Task 3: Verification

- [ ] Run `dotnet build Assembly-CSharp.csproj`.
- [ ] Run `dotnet build LH.Editor.Tests.csproj`.
- [ ] Attempt Unity EditMode tests only if project is not locked.
