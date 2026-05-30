# DrawThisEasy

[![Latest release](https://img.shields.io/github/v/release/robertorenz/DrawThisEasy?label=download&color=0EA5E9&v=1.2.0)](https://github.com/robertorenz/DrawThisEasy/releases/latest)

A fast, native Windows app for sketching **org charts, architecture diagrams, and system flows**. Built with WPF on .NET 9.

Designed for speed: pick a tool, click the canvas, type a label. Drag from any shape to another to connect them. Keyboard shortcuts for everything.

---

## Download

Grab the latest build from the **[Releases page](https://github.com/robertorenz/DrawThisEasy/releases/latest)**:

- **[DrawThisEasy-Setup.exe](https://github.com/robertorenz/DrawThisEasy/releases/latest)** — Windows installer (Start-menu entry, optional desktop shortcut, uninstaller). Recommended.
- **[DrawThisEasy.exe](https://github.com/robertorenz/DrawThisEasy/releases/latest)** — portable single-file build; no install, just download and run.

Both are self-contained — the .NET 9 runtime is bundled, nothing else to install. Windows 10/11, 64-bit. The exes aren't code-signed, so SmartScreen may show an "unknown publisher" prompt on first run (*More info → Run anyway*).

---

## What's new in 1.2.2

- Presentation: **update a stored screen to the current view** — frame an area (pan/zoom) and click ◎ on a screen to set its position and zoom.
- Presentation: a **current-screen indicator** (name · n / total) shows while presenting so you always know where you are.

## What's new in 1.2.1

- Presentation: **rename screens** (double-click in the panel) and choose a **transition style** — zoom out & in, smooth glide, instant cut, or fade through background.

## What's new in 1.2.0

- **Presentation mode** — mark numbered screens, reorder them in a queue, choose a background color, and **Start Presenting** for a fullscreen slideshow that flies between screens with a smooth zoom-out/zoom-in transition. Navigate with arrows / Space / Enter; Esc exits.
- **Go to Free Space** now finds an empty gap and auto-adds it as the next presentation screen.

## What's new in 1.1.0

- **Rich Text shape** — a formatted text card with mixed fonts, sizes, **bold / italic / underline**, colors, and alignment in one block.
- **Label typography** — set font family, size, B/I/U, alignment, and text color per shape from the Inspector.
- **Smarter paste from other apps** — copy from **PowerPoint** (or a browser, or a screenshot): images paste at full resolution, formatted text becomes a Rich Text shape with its formatting preserved, and plain text becomes a Text shape.

---

## Why it exists

When you need to whiteboard *"how do the clients, load balancer, app servers, queue, and database fit together"*, you don't want to wrestle with Visio or wait for a web app to load. DrawThisEasy opens instantly, ships as a single `.exe`, and stays out of your way.

I created this as an easy-to-use drawing app — with simple shapes, so you can quickly show how something works. I took ideas from Excalidraw, but also from programs I've used in the past, like PageMaker and CorelDRAW; the use of rulers and guides, for example, comes from those.

The main push to create it was watching my good friend **Alejandro Elías** always trying to use Windows Paint to show how something works, or should work — drawing boxes and lines, then moving them, deleting, retrying, and so on. My idea was: *I think I can make something easier for him to use and to teach with.*

## Features

- **Multiple documents in tabs** — open several diagrams at once and switch between them along the top of the canvas. Each tab keeps its own zoom, selection, and unsaved-changes state; the `+` (or `Ctrl+N`) opens a new one, and closing a tab with unsaved edits prompts you to save.
- **Menu bar** (File / Edit / View / Language / Help) with full keyboard accelerators.
- **Top tool strip** and **left palette** for fast tool switching.
- **Customizable Favorites toolbar** — pin your most-used tools (right-click any toolbar button → *Add to Favorites*, or **View → Toolbars → Customize Favorites...**) to a second toolbar row. Hide the main toolbar, hide the favorites toolbar, reorder favorite items, or **Reset Toolbars to Defaults** from the View menu at any time.
- **Shape palette** tailored to systems work:
  - Process, Component, Start / End, Decision, Hexagon, Data
  - Database (cylinder), Cloud, Server stack, User, Queue, Sticky note, Text
- **Smart connectors** that snap to shape edges and reroute automatically as shapes move. Sticky connector mode — wire shape → shape → shape without re-selecting the tool. **Right-click a connector** to switch its routing (straight, curved, or elbow) and line style (solid, dashed, dotted); curved connectors get a draggable bézier handle.
- **Click-on-shape selects.** In Connector mode a single click on a shape switches to Select and selects it; only drags create connections.
- **Empty-area drag pans the canvas** in both Select and Connector modes. Hold `Shift` while dragging empty space to marquee-select instead.
- **Inline label editing** — double-click any shape, type, `Enter` to commit.
- **Rich Text shape** — a formatted text card you edit in place (double-click): mix fonts, sizes, **bold / italic / underline**, colors, and alignment within one block using the Inspector controls or `Ctrl+B/I/U`. Pasting formatted text from PowerPoint/Word into the canvas creates one of these with its formatting intact. Stored as RTF in the `.ptd.json`.
- **Pan & zoom** — `Space` + drag, right-click + drag, or just drag empty area. Mouse wheel zooms.
- **Go to Free Space** — a toolbar button (also **View → Go to Free Space**) that finds an empty gap on the canvas, marks it as the next numbered **presentation screen**, and pans there — so you can lay out another scene without overlapping existing content.
- **Presentation mode** — turn a diagram into a slideshow. **Present** opens a panel where you mark screens (capture the current view, or auto-add empty ones), **double-click a screen to rename it**, **update a screen to the current view** (◎ — re-captures its position & zoom), reorder them in a numbered queue, choose a background color, and pick a **transition style** — *zoom out & in*, *smooth glide*, *instant cut*, or *fade through background*. **Start Presenting** goes fullscreen and flies between screens. Navigate with arrow keys (**↑/← back**, **↓/→ forward**), **Space/Enter** (forward), **Home/End** (first/last); **Esc** exits.
- **Inspector panel** for fill / stroke colors, **label typography** (font family, size, **bold / italic / underline**, alignment, and text color — applied to every selected shape), and layer ordering.
- **Rulers, guides & snapping** — rulers along the top and left; **drag from a ruler** onto the canvas to drop a guide line. Drag a guide to reposition it, or **drag it back onto its ruler to remove it** (**View → Clear Guides** clears all). Moving shapes **snap** to other shapes' edges/centers and to guides, with live red alignment lines; hold `Alt` to move freely.
- **Preferences** (Edit → Preferences) — set the **default connector** routing & line style for new connectors, toggle **snapping**, choose **ruler units** (pixels, cm, inches, picas), turn on **autosave** (silently writes every dirty tab that has a file path), and **reopen files on startup** (remembers the set of open documents and reopens them on next launch). Saved to `%LOCALAPPDATA%\DrawThisEasy\preferences.json`; the open-files list lives next to it in `session.json`.
- **Right-click context menu** on any shape — a polished popup to edit text, apply fill / stroke swatches (or pick a custom color), reorder layers, duplicate, copy, and delete. Right-drag empty space still pans.
- **Copy / Cut / Paste** (`Ctrl+C` / `Ctrl+X` / `Ctrl+V`) via the system clipboard, with connection edges preserved. Works between two open DrawThisEasy instances. **Paste also accepts content from other apps**: copy a shape, slide, or image in **PowerPoint** (or a screenshot, or any image from a browser) and `Ctrl+V` drops it onto the canvas as a full-resolution image; copy **formatted text** and it pastes as a **Rich Text** shape with fonts, colors, and styles preserved; plain text pastes as a Text shape. (To paste text *into* an existing shape's label, double-click the shape to edit it and `Ctrl+V` there.)
- **Templates** — a broad starter set: org chart; web / three-tier / full-stack architectures; REST API, serverless, microservices, event-driven; frontend & backend, message middleware, database cluster, caching; client-server, CI/CD, Kubernetes, data pipeline; and a blank canvas.
- **Cloud service objects** — a gallery of **AWS**, **Azure**, and **Google Cloud** services (compute, functions, storage, databases, containers, messaging, networking, analytics, monitoring) drawn as provider-tinted badge tiles. *Generic, original glyphs — not the providers' trademarked icons.* The **AWS / Azure / Google** buttons — in both the toolbar and the left **Cloud** palette group — drop down a flyout of that provider's services (icon + name); click one to place it, no dialog. File → Cloud Services still opens the full browse-all gallery. A **Templates** button is in the toolbar too.
- **Undo / Redo** (`Ctrl+Z` / `Ctrl+Y`).
- **Save / Load** as JSON; **Open** supports selecting **several files at once** (each opens in its own tab); **Open Recent** lists the last 5 files; **Export** as PNG (2× resolution).
- **Export to other tools** (File menu) — **Excalidraw** (`.excalidraw`), **draw.io / diagrams.net** (`.drawio`), and **Mermaid** (`.mmd`) so diagrams open in the editors you already use.
- **Import from Excalidraw** (File → Import from Excalidraw, or just File → Open) — opens `.excalidraw` scenes as diagrams, mapping shapes, labels, **images**, and bound arrows into connections. Select several files to import them as separate tabs.
- **Images** — insert a raster image from the toolbar **Image** button, the left palette, or Edit → Insert Image (or import one from Excalidraw). Images are stored inline (base64) in the `.ptd.json`, so saved files stay self-contained.
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

**Scroll:** mouse wheel (`Shift`+wheel for horizontal), or the scrollbars.
**Pan:** hold `Space` and drag, right-click and drag, or just drag any empty area.
**Zoom:** `Ctrl`+mouse wheel, or the zoom buttons.
**Marquee select:** hold `Shift` and drag empty area.
**Help:** `F1` opens the user manual; `?` opens the keyboard-shortcut reference.

## Build & run

Requires the .NET 9 SDK with the Windows Desktop workload on Windows 10/11.

```powershell
dotnet build DrawThisEasy/DrawThisEasy.csproj
dotnet run --project DrawThisEasy/DrawThisEasy.csproj
```

To produce a single self-contained, compressed `.exe` (~63 MB):

```powershell
dotnet publish DrawThisEasy/DrawThisEasy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
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
