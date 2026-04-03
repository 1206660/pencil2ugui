# design2ugui

> A Unity UGUI conversion scaffold with the previous source-specific importer removed.

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/unity-2021.3%2B-green.svg)](https://unity.com/)

## Current State

This repository now keeps only the Unity-side building blocks:

- `Editor/Models/UguiModels.cs`
- `Editor/Core/UguiConverter.cs`
- `Editor/Core/PrefabCreator.cs`
- `Editor/Core/ProjectSettings.cs`

Removed from the codebase:

- legacy API access
- legacy source node models
- legacy parser and importer flow
- legacy editor window and URL parsing
- legacy source-specific AI analyzers

## Next Direction

This is now the baseline for a future `pencil2ugui` pipeline. The next pieces to add are:

1. Pencil source models
2. Pencil parser
3. CLI-based conversion flow outside Unity
4. Unity-side import entry for Pencil

## Pencil CLI

The repository now includes a direct Pencil conversion CLI:

```bash
node scripts/pencil-to-ugui.mjs --file D:\PencilProject\1.pen --node bi8Au
```

Optional file output:

```bash
node scripts/pencil-to-ugui.mjs --file D:\PencilProject\1.pen --node bi8Au --out output\screen.json
```

Tests:

```bash
node --test tests/pencil-to-ugui.test.mjs
```

For Unity import bundle generation:

```bash
node scripts/pencil-to-unity-bundle.mjs --file D:\PencilProject\1.pen --node bi8Au --out output\bi8Au.bundle.json
```

Bundle output conventions:

- screen prefab: `Assets/UI/<ScreenName>/<ScreenName>.prefab`
- component prefabs: `Assets/UI/Components/<ComponentName>.prefab`
- sprites: `Assets/UI/Sprites/...`
