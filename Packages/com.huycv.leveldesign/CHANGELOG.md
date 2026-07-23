# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-23

First release as a distributable UPM package. Previously the tool lived under
`Assets/_TheGame/LevelDesign` inside the Block Loop project.

### Added

- Project Settings page **Level Design (Huycv)** for the levels folder, the screenshots
  folder, and the colour palette lookup, stored per project in `EditorPrefs`.
- **Output Folders…** button in the Level I/O panel that opens that page.
- Automatic fallback to the legacy `Assets/_TheGame/Levels` and
  `Assets/_TheGame/Screenshots` folders when they exist and no preference is set.

### Changed

- Namespace and assembly renamed from `BlockLoop.LevelDesign` / `_BlockLoop.LevelDesign`
  to `Huycv.LevelDesign`, so the package cannot collide with a host project's own types.
- Menu entry renamed from `Tools/Level/Level Design` to `Tools/Level/Level Design (Huycv)`,
  and the window tab title along with it.
- Levels and screenshots folders are now configurable instead of hardcoded to
  `Assets/_TheGame/…`.

### Removed

- Assembly reference to `Runtime.Game`. The colour palette is now loaded as a plain
  `ScriptableObject` and read through `SerializedObject`, so the package compiles standalone
  in any project.
