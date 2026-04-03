# pencil2ugui

> A Unity UGUI conversion scaffold with a direct Pencil-to-UGUI conversion path.

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/unity-2021.3%2B-green.svg)](https://unity.com/)

## Current State

The Unity package payload now lives under:

- `Packages/com.pencil2ugui.core/package.json`
- `Packages/com.pencil2ugui.core/Editor/...`
- `Packages/com.pencil2ugui.core/scripts/...`

Removed from the codebase:

- legacy API access
- legacy source node models
- legacy parser and importer flow
- legacy editor window and URL parsing
- legacy source-specific AI analyzers

## Next Direction

This is now the baseline for a fuller `pencil2ugui` pipeline. The next pieces to add are:

1. Pencil source models
2. Pencil parser
3. CLI-based conversion flow outside Unity
4. Unity-side import entry for Pencil

## Pencil CLI

The repository now includes a direct Pencil conversion CLI:

```bash
node Packages/com.pencil2ugui.core/scripts/pencil-to-ugui.mjs --file D:\PencilProject\1.pen --node bi8Au
```

Fixture example from this repo:

```bash
node Packages/com.pencil2ugui.core/scripts/pencil-to-ugui.mjs --file tests/fixtures/sample.pen --node root
```

Optional file output:

```bash
node Packages/com.pencil2ugui.core/scripts/pencil-to-ugui.mjs --file D:\PencilProject\1.pen --node bi8Au --out output\screen.json
```

Tests:

```bash
node --test tests/pencil-to-ugui.test.mjs
```

For Unity import bundle generation:

```bash
node Packages/com.pencil2ugui.core/scripts/pencil-to-unity-bundle.mjs --file D:\PencilProject\1.pen --node bi8Au --out output\bi8Au.bundle.json
```

Fixture example:

```bash
node Packages/com.pencil2ugui.core/scripts/pencil-to-unity-bundle.mjs --file tests/fixtures/sample.pen --node root --out output/sample.bundle.json
```

Bundle output conventions:

- screen prefab: `Assets/UI/<ScreenName>/<ScreenName>.prefab`
- component prefabs: `Assets/UI/Components/<ComponentName>.prefab`
- sprites: `Assets/UI/Sprites/...`

## Unity Import Flow

For restoring a selected Pencil interface directly into Unity:

1. Open `Tools/Design2Ugui/Import Pencil Selection`
2. Choose the `.pen` file
3. Paste the selected Pencil node ID
4. Click `Import Selected Interface`

The editor window generates a temporary bundle via `Packages/com.pencil2ugui.core/scripts/pencil-to-unity-bundle.mjs` and immediately imports the resulting screen prefab, component prefabs, and sprite assets into Unity.
