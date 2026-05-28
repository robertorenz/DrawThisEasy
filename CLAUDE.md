# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

DrawThisEasy is a native Windows diagramming app (org charts, architecture diagrams, system flows) built with **WPF on .NET 9** (`net9.0-windows`, `UseWPF`, nullable + implicit usings enabled). Single-project solution under `DrawThisEasy/`. No tests, no external NuGet dependencies — everything is hand-rolled on top of WPF primitives.

## Build & run

```powershell
dotnet build DrawThisEasy/DrawThisEasy.csproj
dotnet run --project DrawThisEasy/DrawThisEasy.csproj
```

Single self-contained `.exe` (output in `DrawThisEasy/bin/Release/net9.0-windows/win-x64/publish/`, then copied to `run/DrawThisEasy.exe` — that's the folder the user actually launches from):

```powershell
dotnet publish DrawThisEasy/DrawThisEasy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
cp DrawThisEasy/bin/Release/net9.0-windows/win-x64/publish/DrawThisEasy.exe run/DrawThisEasy.exe
```

`EnableCompressionInSingleFile` is mandatory — it cuts the exe from ~134 MB to ~63 MB and the user expects the compressed build.

Requires the .NET 9 SDK with the Windows Desktop workload. There is no lint config and no test project, so `dotnet build` is the only quality gate — treat build warnings as the signal.

## Architecture

The app is **model + custom-canvas + thin window**, not MVVM. There are no view-models or data bindings to the model; the canvas mutates the model directly and raises plain events that `MainWindow` listens to.

- **`Models/Models.cs`** — the entire data model and it's deliberately tiny: `DiagramModel` (title + `List<ShapeNode>` + `List<Connection>`), `ShapeNode`, `Connection`, the `ShapeKind` and `ToolMode` enums, `ToolModeMap.ShapeForTool` (maps an "Add" tool to the shape it creates), and `Palette` (the approved fill/stroke hex arrays). This is the JSON-serialized shape, so changing it changes the save-file format.

- **`Controls/DiagramCanvas.cs`** — the heart of the app (~1250 lines). A custom `Canvas` subclass that owns:
  - **Layered child canvases** inside a single `_world` canvas driven by one `MatrixTransform` (`_worldTransform`): `_connLayer` → `_shapeLayer` → `_overlayLayer`. Pan/zoom is just mutation of that matrix; the dotted-grid background is a `DrawingBrush` whose `Viewport` is offset to follow the transform.
  - **Visual mirrors** of the model: `_shapeVisuals` (`Dictionary<id, ShapeVisual>`) and `_connVisuals`. `ShapeVisual` and `ConnectionVisual` are defined at the bottom of this same file. When the model changes you must keep these dictionaries in sync (add/remove visuals), or call `Rebuild()` to regenerate everything from the model.
  - **All mouse/keyboard interaction** via a `DragMode` state machine (`None/Pan/MoveShape/ResizeShape/ConnectDrag/Marquee`). The outer canvas captures the mouse on press, so **shape elements never get their own MouseDown handlers** — hit-testing is done manually in world coordinates (`HitTestShape`, `ScreenToWorld`). See the comment in `AddShapeVisual`. Double-click is detected centrally via `e.ClickCount` in `OnMouseLeftDown`.
  - **Undo/redo** via whole-model JSON snapshots: `Snapshot()` pushes `Persistence.ToJson(_model)` onto `_undoStack` (capped at 100) and clears redo. **Any mutating public method must call `Snapshot()` first.** Undo/redo deserialize a snapshot back into `_model` and call `Rebuild()`.
  - **Clipboard** as JSON on the system clipboard under the private format string `DrawThisEasy.Clipboard.v1`, so copy/paste works between two running instances. Pasted shapes get fresh IDs (an `idMap` rewrites connection endpoints).
  - The canvas exposes the model + events (`SelectionChanged`, `ToolChanged`, `ZoomChanged`, `ModelDirty`) and `CurrentTool`; `MainWindow` is purely reactive to these.

- **`Controls/ShapeFactory.cs`** — pure geometry. `BuildBody(kind, w, h, fill, stroke)` returns a container plus the array of `Shape`s whose Fill/Stroke get re-themed on selection/recolor. `EdgeIntersect(node, externalPoint)` computes where a connector attaches to a shape's edge (special-cased per `ShapeKind`: ellipse, diamond, else bounding box) — this is what makes connectors snap to edges and reroute as shapes move (`RouteConnection`).

- **`Controls/ShapeIcons.cs`** — palette/tool-strip icons drawn in code (keyed by `ToolMode`).

- **`MainWindow.xaml(.cs)`** — builds the top tool strip, left palette, and color swatches **in code** (`BuildToolStrip`/`BuildPalette`/`BuildSwatches`), not in XAML. Multiple `ToggleButton`s can map to the same `ToolMode` (palette + strip), tracked in `_toolButtons`; `SyncToolButtons` keeps them consistent. Owns all menu/keyboard command handlers and delegates to `Diagram` (the `DiagramCanvas` instance). Keyboard: `Window_KeyDown` handles Ctrl+N/O/S/E and `?`, then forwards to `Diagram.HandleKeyDown`; both bail out when focus is in a `TextBox` (label editing).

- **`Services/`** — `Persistence` (JSON load/save, camelCase, indented — the canonical (de)serializer used everywhere including undo), `Templates` (built-in starter `DiagramModel`s, cloned on use so the originals stay pristine), `Exporter` (PNG via `RenderTargetBitmap` at 2× scale), `L10n` (see below).

- **`Dialogs/`** — `ModalWindow` (reusable Info/Confirm via static `ModalWindow.Info(...)` / `ModalWindow.Confirm(...)`), `HelpWindow`, `TemplateGalleryWindow`, `ColorPickerWindow` (HSV picker, static `ColorPickerWindow.Pick(owner, initial)`).

## Conventions that matter

- **Localization is mandatory for user-facing text.** Every visible string goes through `L10n.T("some.key")` and must have an entry in **both** the `Language.En` and `Language.Es` dictionaries in `Services/L10n.cs`. Setting `L10n.Current` fires `LanguageChanged`; `MainWindow.ApplyLanguage()` re-translates every control live (no restart). When you add UI, add the key to both dictionaries and re-translate it in `ApplyLanguage` (store control refs the way the existing `_toolLabelTb` / `_groupLabels` / `_toolTipKey` maps do).

- **Theme brushes/styles live in `Resources/Theme.xaml`** (merged in `App.xaml`) and are pulled with `FindResource("...")`. Keys: `AccentBrush`/`AccentStrongBrush`/`AccentSoftBrush` (sky `#0EA5E9`), `TealBrush`, `AmberBrush`, `DangerBrush`, `BgBrush`/`CanvasBgBrush`/`SidebarBgBrush`, `BorderBrush`, `TextBrush`/`TextMutedBrush`, button styles `PrimaryButton`/`GhostButton`/`ToolButton`/`StripToolButton`, `GroupLabel`. **Palette is slate / sky / teal / amber — no purple.** New shape colors must come from `Palette.Fills` / `Palette.Strokes`.

- **No `MessageBox` for user dialogs** — use `ModalWindow.Info/Confirm`. (`MessageBox` appears only in `App.xaml.cs`'s last-resort crash handler.)

- **Mutations snapshot then sync visuals.** The pattern in `DiagramCanvas` for any edit is: `Snapshot()` → mutate `_model` → update the matching `ShapeVisual`/`ConnectionVisual` (or `Rebuild()`) → reroute affected connections (`RouteConnectionsFor`) → `RebuildOverlay()` → raise `SelectionChanged`. Follow it or undo/redo and the on-screen state will drift from the model.

- **Save format** is `*.ptd.json` (plain JSON). The `Version` field on `DiagramModel` exists for forward-compat but nothing branches on it yet.

- **Crash safety:** `App.xaml.cs` hooks dispatcher/domain/task unhandled exceptions, appends a stack trace to `%LOCALAPPDATA%\DrawThisEasy\DrawThisEasy-crash.log`, and shows a dialog instead of exiting silently.

## Adding a new shape kind (touch-list)

1. Add the value to `ShapeKind` and a matching `Add*` to `ToolMode` + the mapping in `ToolModeMap.ShapeForTool` (`Models.cs`).
2. Add a `BuildXxx` case to `ShapeFactory.BuildBody`, a default size in `DiagramCanvas.DefaultSize`, and a default-label key in `DiagramCanvas.DefaultLabel`. Special-case `EdgeIntersect` if it isn't box-shaped.
3. Add an icon case in `ShapeIcons.GetIcon`.
4. Register it in the palette/strip lists in `MainWindow` (`BuildPalette`/`BuildToolStrip`) and add its tip/label keys to `TipKeyForTool`/`LabelKeyForTool`.
5. Add the label/tip/default-label strings to **both** language dictionaries in `L10n.cs`.
6. Optionally bind a keyboard shortcut in `DiagramCanvas.HandleKeyDown`.
</content>
</invoke>
