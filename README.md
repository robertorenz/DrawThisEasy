# DrawThisEasy

A fast, native Windows app for sketching **org charts, architecture diagrams, and system flows**. Built with WPF on .NET 9.

Designed for speed: pick a tool, click the canvas, type a label. Drag from any shape to another to connect them. Keyboard shortcuts for everything.

---

## Why it exists

When you need to whiteboard *"how do the clients, load balancer, app servers, queue, and database fit together"*, you don't want to wrestle with Visio or wait for a web app to load. DrawThisEasy opens instantly, ships as a single `.exe`, and stays out of your way.

## Features

- **Multiple documents in tabs** — open several diagrams at once and switch between them along the top of the canvas. Each tab keeps its own zoom, selection, and unsaved-changes state; the `+` (or `Ctrl+N`) opens a new one, and closing a tab with unsaved edits prompts you to save.
- **Menu bar** (File / Edit / View / Language / Help) with full keyboard accelerators.
- **Top tool strip** and **left palette** for fast tool switching.
- **Shape palette** tailored to systems work:
  - Process, Component, Start / End, Decision, Hexagon, Data
  - Database (cylinder), Cloud, Server stack, User, Queue, Sticky note, Text
- **Smart connectors** that snap to shape edges and reroute automatically as shapes move. Sticky connector mode — wire shape → shape → shape without re-selecting the tool.
- **Click-on-shape selects.** In Connector mode a single click on a shape switches to Select and selects it; only drags create connections.
- **Empty-area drag pans the canvas** in both Select and Connector modes. Hold `Shift` while dragging empty space to marquee-select instead.
- **Inline label editing** — double-click any shape, type, `Enter` to commit.
- **Pan & zoom** — `Space` + drag, right-click + drag, or just drag empty area. Mouse wheel zooms.
- **Inspector panel** for fill / stroke colors and layer ordering.
- **Right-click context menu** on any shape — a polished popup to edit text, apply fill / stroke swatches (or pick a custom color), reorder layers, duplicate, copy, and delete. Right-drag empty space still pans.
- **Copy / Cut / Paste** (`Ctrl+C` / `Ctrl+X` / `Ctrl+V`) via the system clipboard, with connection edges preserved. Works between two open DrawThisEasy instances.
- **Templates** — Org chart, Web architecture, Client-server, Microservices, Data pipeline, Blank.
- **Cloud service objects** — a gallery of **AWS**, **Azure**, and **Google Cloud** services (compute, functions, storage, databases, containers, messaging, networking, analytics, monitoring) drawn as provider-tinted badge tiles. *Generic, original glyphs — not the providers' trademarked icons.* The **AWS / Azure / Google** buttons — in both the toolbar and the left **Cloud** palette group — drop down a flyout of that provider's services (icon + name); click one to place it, no dialog. File → Cloud Services still opens the full browse-all gallery. A **Templates** button is in the toolbar too.
- **Undo / Redo** (`Ctrl+Z` / `Ctrl+Y`).
- **Save / Load** as JSON; **Export** as PNG (2× resolution).
- **Export to other tools** (File menu) — **Excalidraw** (`.excalidraw`), **draw.io / diagrams.net** (`.drawio`), and **Mermaid** (`.mmd`) so diagrams open in the editors you already use.
- **Unsaved-changes guard** — closing the app with pending edits prompts you to **Save**, **Don't save**, or **Cancel**; the title bar shows a `•` whenever there are unsaved changes.
- **Modal dialogs** instead of system message boxes — clean, on-brand prompts.
- **Bilingual UI** — English / Español, switchable live (no restart) from the Language menu or one-click EN/ES toggle in the top bar.
- **Built-in user manual** (Help → User Manual, or `F1`) and a keyboard-shortcut reference (Help → Keyboard Shortcuts, or `?`) — both fully localized.
- **Global crash handler** writes a diagnostic log to `%LOCALAPPDATA%\DrawThisEasy\DrawThisEasy-crash.log` and shows a dialog instead of silently exiting.
- Professional palette: slate / sky / teal / amber. **No purple.**

## Keyboard shortcuts

| Tools          |                | Editing         |                 |
|----------------|----------------|-----------------|-----------------|
| `V`            | Select         | `Ctrl+Z` / `Y`  | Undo / Redo     |
| `L`            | Connector      | `Ctrl+C` / `X` / `V` | Copy / Cut / Paste |
| `R`            | Rectangle      | `Ctrl+D`        | Duplicate       |
| `O`            | Rounded        | `Ctrl+A`        | Select all      |
| `E`            | Ellipse        | `Del`           | Delete          |
| `D`            | Decision       | Double-click    | Edit label      |
| `H`            | Hexagon        | `Esc`           | Cancel          |
| `B`            | Database       |                 |                 |
| `C`            | Cloud          | **File**        |                 |
| `S`            | Server         | `Ctrl+N`        | New tab         |
| `P`            | User / Person  | `Ctrl+O`        | Open            |
| `T`            | Text           | `Ctrl+S`        | Save            |
|                |                | `Ctrl+E`        | Export PNG      |

**Pan:** hold `Space` and drag, right-click and drag, or just drag any empty area.
**Zoom:** mouse wheel.
**Marquee select:** hold `Shift` and drag empty area.
**Help:** `F1` opens the user manual; `?` opens the keyboard-shortcut reference.

## Build & run

Requires the .NET 9 SDK with the Windows Desktop workload on Windows 10/11.

```powershell
dotnet build DrawThisEasy/DrawThisEasy.csproj
dotnet run --project DrawThisEasy/DrawThisEasy.csproj
```

To produce a single self-contained `.exe`:

```powershell
dotnet publish DrawThisEasy/DrawThisEasy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output lands in `DrawThisEasy/bin/Release/net9.0-windows/win-x64/publish/`.

## Project layout

```
DrawThisEasy/
├── App.xaml(.cs)                  — App shell, merged Theme.xaml, global crash handler
├── MainWindow.xaml(.cs)           — Top bar (menu + brand + zoom), tool strip, palette, status
├── Controls/
│   ├── DiagramCanvas.cs           — Custom Canvas: selection, drag, resize, connect,
│   │                                 pan / zoom, marquee, undo, clipboard, text edit
│   ├── ShapeFactory.cs            — Builds the WPF visuals for each shape kind +
│   │                                 edge-intersect math for connector snapping
│   └── ShapeIcons.cs              — Palette icons rendered in code
├── Dialogs/
│   ├── ModalWindow.xaml(.cs)      — Reusable modal (Info / Confirm)
│   ├── HelpWindow.xaml(.cs)       — Localized keyboard-shortcut reference
│   ├── ManualWindow.xaml(.cs)     — Localized user manual (Help → User Manual / F1)
│   └── TemplateGalleryWindow…     — Template picker with live previews
├── Models/Models.cs               — DiagramModel, ShapeNode, Connection, enums, palette
├── Resources/Theme.xaml           — Brushes, button + menu styles, fonts
└── Services/
    ├── L10n.cs                    — Tiny string table + LanguageChanged event (EN / ES)
    ├── Persistence.cs             — JSON load / save
    ├── Templates.cs               — Built-in starter diagrams
    └── Exporter.cs                — PNG export via RenderTargetBitmap
```

## License

[MIT](LICENSE) © Roberto Renz.
