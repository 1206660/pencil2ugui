# CLAUDE.md

## Project Overview

This repository is a stripped-down UGUI conversion scaffold. All previous source-specific importer logic has been removed.

## Remaining Core Files

- `Editor/Models/UguiModels.cs`
- `Editor/Core/UguiConverter.cs`
- `Editor/Core/PrefabCreator.cs`
- `Editor/Core/ProjectSettings.cs`

## Expected Direction

Future source-specific logic, such as Pencil parsing, should stay isolated from the Unity conversion layer.
