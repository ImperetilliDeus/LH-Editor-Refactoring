# Opening Prefabs And Restore Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist and restore opening prefab choices, apply default wall material on loaded walls, and prevent invalid restored handles.

**Architecture:** Keep the existing work-state schema compatible by using existing opening type keys and adding an explicit prefab key. Extend existing manager services instead of adding a new persistence layer. Restore visuals through the current opening rebuild and wall factory paths.

**Tech Stack:** Unity C#, ScriptableObject catalogs, JsonUtility work-state schema, existing wall/opening/furniture managers.

---

### Task 1: Opening Catalog Prefabs

**Files:**
- Modify: `Assets/Scripts/Draw/Wall/Opening/OpeningTypeCatalog.cs`
- Modify: `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.TypeCatalog.cs`
- Modify: `Assets/Scripts/Draw/Wall/Opening/WallOpeningPlacementManager.Visuals.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateSchema.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateBuilder.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`

- [ ] Add prefab fields to catalog items.
- [ ] Add explicit `prefabKey` to saved opening DTOs while keeping old type-key fallback.
- [ ] Resolve catalog item by saved key on load and apply model prefab during opening visual rebuild.

### Task 2: Wall Material Fallback

**Files:**
- Modify: `Assets/Scripts/Draw/Wall/Core/DrawManager.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStatePersistenceController.cs`
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`

- [ ] Expose DrawManager wall material through a read-only property.
- [ ] Pass DrawManager through load services.
- [ ] Use that material in restored wall visual state when no saved material exists.

### Task 3: Ghost Handle Guard

**Files:**
- Modify: `Assets/Scripts/Draw/Wall/Core/HandleManager.cs`

- [ ] Skip invalid wall endpoints during registration.
- [ ] Remove empty vertex groups before handle UI positioning.
- [ ] Rebuild from hierarchy after load using only valid walls.

### Task 4: Verification

- [ ] Run `dotnet build Assembly-CSharp.csproj`.
- [ ] Run `dotnet build LH.Editor.Tests.csproj`.
- [ ] Attempt Unity EditMode tests if the project is not locked by another Unity instance.
