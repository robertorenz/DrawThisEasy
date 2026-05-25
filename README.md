# PictureThis

A fast, native Windows app for sketching **org charts, architecture diagrams, and system flows**. Built with WPF on .NET 9.

Designed for speed: pick a tool, click the canvas, type a label. Drag from any shape to another to connect them. Keyboard shortcuts for everything.

---

## Why it exists

When you need to whiteboard "how do the clients, load balancer, app servers, queue, and database fit together," you don't want to wrestle with Visio or wait for a web app to load. PictureThis opens instantly, ships as a single `.exe`, and stays out of your way.

## Features

- **Shape palette** tailored to systems work: process, decision, database (cylinder), cloud, server, user, queue, sticky note, plus the standard rectangle / rounded / ellipse / diamond / hexagon / parallelogram / text.
- **Smart connectors** that snap to shape edges and reroute automatically as shapes move.
- **Pan & zoom** (Space + drag, right-click + drag, mouse wheel).
- **Inline label editing** — double-click any shape.
- **Inspector panel** for fill / stroke colors and layer ordering.
- **Templates** — start from an org chart, web architecture, microservices, data pipeline, etc.
- **Undo / redo** (`Ctrl+Z` / `Ctrl+Y`).
- **Save / load** as JSON; **export** as PNG (2× resolution).
- **Modal dialogs** instead of system message boxes — clean, on-brand prompts.
- Professional palette: slate / sky / teal / amber. No purple.

## Keyboard shortcuts

| Tools         |               | Editing         |               |
|---------------|---------------|-----------------|---------------|
| `V`           | Select        | `Ctrl+Z` / `Y`  | Undo / redo   |
| `L`           | Connector     | `Ctrl+D`        | Duplicate     |
| `R`           | Rectangle     | `Ctrl+A`        | Select all    |
| `O`           | Rounded       | `Del`           | Delete        |
| `E`           | Ellipse       | Double-click    | Edit label    |
| `D`           | Decision      | `Esc`           | Cancel        |
| `H`           | Hexagon       |                 |               |
| `B`           | Database      | **File**        |               |
| `C`           | Cloud         | `Ctrl+N`        | New diagram   |
| `S`           | Server        | `Ctrl+O`        | Open          |
| `P`           | User / Person | `Ctrl+S`        | Save          |
| `T`           | Text          | `Ctrl+E`        | Export PNG    |

**Pan:** hold `Space` and drag (or right-click and drag).
**Zoom:** mouse wheel.

## Build & run

Requires .NET 9 SDK with the Windows Desktop workload on Windows 10/11.

```powershell
dotnet build PictureThis/PictureThis.csproj
dotnet run --project PictureThis/PictureThis.csproj
```

To produce a single self-contained `.exe`:

```powershell
dotnet publish PictureThis/PictureThis.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output lands in `PictureThis/bin/Release/net9.0-windows/win-x64/publish/`.

## Project layout

```
PictureThis/
├── App.xaml                       — App shell + merged Theme.xaml
├── MainWindow.xaml(.cs)           — Top bar, palette, status, hosts DiagramCanvas
├── Controls/
│   ├── DiagramCanvas.cs           — Custom Canvas: selection, drag, resize, connect, pan/zoom, undo
│   ├── ShapeFactory.cs            — Builds the WPF visuals for each shape kind + edge-intersect math
│   └── ShapeIcons.cs              — 18×18 palette icons rendered in code
├── Dialogs/
│   ├── ModalWindow.xaml(.cs)      — Reusable modal (Info / Confirm)
│   ├── HelpWindow.xaml(.cs)       — Keyboard shortcut reference
│   └── TemplateGalleryWindow…     — Template picker with live previews
├── Models/Models.cs               — DiagramModel, ShapeNode, Connection, enums, color palette
├── Resources/Theme.xaml           — Brushes, button styles, fonts
└── Services/
    ├── Persistence.cs             — JSON load / save
    ├── Templates.cs               — Built-in starter diagrams
    └── Exporter.cs                — PNG export via RenderTargetBitmap
```

## License

This is a personal tool. Use freely; no warranty.
