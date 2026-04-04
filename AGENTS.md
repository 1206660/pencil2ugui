# Repository Guidelines

## Project Structure & Module Organization
This repository is a Unity project centered on UI conversion workflows.

- `Assets/` contains game content and editor entry points. Key folders: `Assets/Art/UIPanel/` for UI prefabs, `Assets/Editor/` for menu commands such as `UnityToPencilBatchExport.cs`.
- `Packages/com.pencil2ugui.core/` is the main package. Use `Editor/Core/` for pipeline logic, `Editor/Models/` for shared data contracts, and `scripts/` for Node-based `.mjs` converters.
- `docs/` stores workflow and schema documents for the Unity to Pencil pipeline.
- `ProjectSettings/` and `Packages/manifest.json` are Unity configuration files. Do not edit casually.
- Generated output belongs under `Temp/`, especially `Temp/PencilBundles/`.

## Build, Test, and Development Commands
- Open locally: `Pencil2Unity.sln` or the Unity project root in Unity Editor.
- Re-run package scripts manually:
  - `node Packages/com.pencil2ugui.core/scripts/write-pen-file.mjs --components <...> --screens <...> --audit <...> --out <...>`
  - `node --check Packages/com.pencil2ugui.core/scripts/unity-to-pencil-pen-lib.mjs`
- Unity batch export:
  - `Unity.exe -batchmode -projectPath D:\UnityProjects\Pencil2Unity -executeMethod Pencil2Unity.Editor.UnityToPencilBatchExport.ExportPen -quit`
- Preferred editor flow: `Tools/Design2Ugui/Export Unity To Pencil (.pen)`.

## Coding Style & Naming Conventions
- C# uses 4-space indentation, PascalCase for types/methods, camelCase for locals/fields.
- Keep editor code under `Packages/com.pencil2ugui.core/Editor/` unless it is a Unity menu entry in `Assets/Editor/`.
- Preserve Unity asset paths and `.meta` files. Never rename assets outside Unity.
- Node scripts are ES modules (`.mjs`), use small pure helpers, and keep file output deterministic.

## Testing Guidelines
- There is no formal automated test suite yet. Minimum validation is:
  - `node --check` for changed `.mjs` files
  - Unity recompiles without editor errors
  - Re-run the relevant export/import flow and inspect generated files in `Temp/PencilBundles/`
- When fixing pipeline bugs, verify both the generated `.pen` and copied `images/` assets.

## Commit & Pull Request Guidelines
- Use conventional commit style when possible, e.g. `feat: add unity to pencil export pipeline`, `fix: avoid duplicate pen ids`.
- Keep commits focused. Do not mix Unity asset churn with pipeline code unless required.
- PRs should include:
  - what changed
  - how it was verified
  - affected paths or menu entries
  - screenshots or sample output paths when UI/export behavior changed

## Security & Configuration Tips
- Never commit secrets, local absolute machine-specific credentials, or generated `Library/` content.
- Treat `Temp/` output as disposable; do not rely on it as source of truth.
