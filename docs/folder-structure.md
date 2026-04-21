# Folder Structure

## Goal

After the recent refactor, the project now has clearer boundaries between gameplay/editor code, project assets, third-party packages, and archival data. This document defines the folder layout we should keep going forward.

## Root Rules

- Keep Unity-generated folders at the repository root only: `Library`, `Temp`, `Logs`, `UserSettings`.
- Keep project documentation under `docs`.
- Keep one-off maintenance scripts under `tools`.
- Keep temporary backups and imported library snapshots out of source control under `_library_backups`.

## Assets Rules

### Runtime and editor-owned content

- `Assets/Scripts`
  - Project-authored C# code only.
  - Organize primarily by feature/domain, not by technical type.
- `Assets/Scenes`
  - Unity scenes owned by this project.
- `Assets/Prefabs`, `Assets/Sprites`, `Assets/Materials`, `Assets/Fonts`, `Assets/Textures`
  - Project-authored assets only.

### Third-party content

- `Assets/ThirdParty`
  - External libraries, plugins, and vendor sample content.
  - Current examples:
    - `ACadSharp`
    - `CSUtilities`
    - `Paroxe`
    - `Plugins`

Third-party assets should not be mixed into project-owned folders unless Unity import requirements force it.

## Script Structure

`Assets/Scripts` should stay feature-first:

- `Draw`
- `Room`
- `Overlay`
- `Import`
- `Export`
- `Furniture`
- `Input`
- `UI`
- `Editor`
- `Camera`

### Placement Rules

- Put shared input abstractions in `Input`.
- Keep wall editing logic grouped under `Draw/Wall`.
- Keep scene/editor-only tooling in `Editor`.
- Avoid creating new top-level script folders unless the feature is independently meaningful.

## Cleanup Targets

These are still worth addressing in follow-up passes:

- Reduce top-level asset folder sprawl where a feature-specific subtree would be clearer.
- Decide whether `Prefabs/Furniture/Models` should become a more explicit catalog structure.
- Review vendor example assets inside `Assets/ThirdParty/Paroxe/PDFRenderer/Examples` and remove them if they are not needed.
- Normalize naming and encoding across docs so Korean text is consistently readable in UTF-8.
