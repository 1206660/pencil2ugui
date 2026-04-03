# CLAUDE.md

## Project Overview

This repository is a stripped-down UGUI conversion scaffold. All previous source-specific importer logic has been removed.

## Unity Package Layout

- `Packages/com.pencil2ugui.core/package.json`
- `Packages/com.pencil2ugui.core/Editor/`
- `Packages/com.pencil2ugui.core/scripts/`

## Expected Direction

Future source-specific logic, such as Pencil parsing, should stay isolated from the Unity conversion layer.
