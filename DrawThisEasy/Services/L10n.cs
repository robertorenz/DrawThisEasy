using System;
using System.Collections.Generic;

namespace DrawThisEasy.Services;

public enum Language { En, Es }

/// Tiny localization service. Subscribe to LanguageChanged to refresh UI strings live.
public static class L10n
{
    public static event EventHandler? LanguageChanged;

    private static Language _current = Language.En;
    public static Language Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void Toggle() => Current = Current == Language.En ? Language.Es : Language.En;

    /// Two-letter code for the *current* language (for the toggle button).
    public static string CurrentCode => Current == Language.En ? "EN" : "ES";

    public static string T(string key)
    {
        if (_strings.TryGetValue(_current, out var dict) && dict.TryGetValue(key, out var v))
            return v;
        // English fallback
        if (_strings.TryGetValue(Language.En, out var en) && en.TryGetValue(key, out var ev))
            return ev;
        return key;
    }

    private static readonly Dictionary<Language, Dictionary<string, string>> _strings = new()
    {
        [Language.En] = new()
        {
            // Top bar
            ["topbar.new"]       = "New",
            ["topbar.templates"] = "Templates",
            ["topbar.cloud"]     = "Cloud",
            ["topbar.image"]     = "Image",
            ["topbar.templates.tip"] = "Browse templates",
            ["topbar.cloud.tip"]     = "Cloud services",
            ["topbar.image.tip"]     = "Insert image",
            ["topbar.open"]      = "Open",
            ["topbar.save"]      = "Save",
            ["topbar.export"]    = "Export",
            ["topbar.help.tip"]  = "Keyboard shortcuts",
            ["topbar.lang.tip"]  = "Language",
            ["topbar.zoom.in"]   = "Zoom in",
            ["topbar.zoom.out"]  = "Zoom out",
            ["topbar.zoom.reset"]= "Reset zoom",

            // Palette groups
            ["group.tools"] = "Tools",
            ["group.shapes"] = "Shapes",
            ["group.infra"] = "Infrastructure",
            ["group.cloud"] = "Cloud",

            // Tool labels (palette text)
            ["tool.select"]    = "Select",
            ["tool.connect"]   = "Connector",
            ["tool.pan"]       = "Pan",
            ["tool.process"]   = "Process",
            ["tool.component"] = "Component",
            ["tool.startend"]  = "Start / End",
            ["tool.decision"]  = "Decision",
            ["tool.hexagon"]   = "Hexagon",
            ["tool.data"]      = "Data",
            ["tool.database"]  = "Database",
            ["tool.cloud"]     = "Cloud",
            ["tool.server"]    = "Server",
            ["tool.user"]      = "User",
            ["tool.queue"]     = "Queue",
            ["tool.note"]      = "Note",
            ["tool.text"]      = "Text",

            // Tooltips (palette + top strip)
            ["tip.select"]        = "Select & move (V)",
            ["tip.connect"]       = "Connect shapes (L)",
            ["tip.pan"]           = "Pan canvas (Space + drag)",
            ["tip.rectangle"]     = "Rectangle / Process (R)",
            ["tip.rounded"]       = "Rounded / Component (O)",
            ["tip.ellipse"]       = "Ellipse / Start-End (E)",
            ["tip.diamond"]       = "Decision (D)",
            ["tip.hexagon"]       = "Hexagon (H)",
            ["tip.parallelogram"] = "Parallelogram / Data",
            ["tip.cylinder"]      = "Database (B)",
            ["tip.cloud"]         = "Cloud service (C)",
            ["tip.server"]        = "Server (S)",
            ["tip.person"]        = "User / Person (P)",
            ["tip.queue"]         = "Queue / stream",
            ["tip.note"]          = "Sticky note",
            ["tip.text"]          = "Text label (T)",

            // Palette hint footer
            ["palette.hint"] = "Click a tool, then click the canvas.\nDrag from a shape's edge in Connector mode to link two shapes.",

            // Status bar (tool display names + hints)
            ["status.add.prefix"] = "Add ",
            ["status.hint.select"]   = "Click to select. Drag to move. Double-click to edit text. Space + drag to pan.",
            ["status.hint.connect"]  = "Click a shape, drag to another shape to connect them.",
            ["status.hint.pan"]      = "Drag the canvas to pan.",
            ["status.hint.add"]      = "Click the canvas to drop the shape.",

            // Default labels stamped on newly added shapes
            ["default.process"]   = "Process",
            ["default.component"] = "Component",
            ["default.start"]     = "Start",
            ["default.decision"]  = "Decision",
            ["default.step"]      = "Step",
            ["default.data"]      = "Data",
            ["default.database"]  = "Database",
            ["default.cloud"]     = "Cloud Service",
            ["default.server"]    = "Server",
            ["default.user"]      = "User",
            ["default.queue"]     = "Queue",
            ["default.note"]      = "Note",
            ["default.text"]      = "Text",

            // Inspector
            ["inspector.fill"]   = "FILL",
            ["inspector.stroke"] = "STROKE",
            ["inspector.front"]  = "Front",
            ["inspector.back"]   = "Back",
            ["inspector.dup"]    = "Dup",
            ["inspector.delete"] = "Delete",
            ["inspector.tip.front"]  = "Bring to front",
            ["inspector.tip.back"]   = "Send to back",
            ["inspector.tip.dup"]    = "Duplicate (Ctrl+D)",
            ["inspector.tip.delete"] = "Delete (Del)",

            // Templates dialog
            ["templates.title"]    = "Start from a template",
            ["templates.subtitle"] = "Pick a starting point. You can edit everything afterwards.",
            ["templates.cancel"]   = "Cancel",
            ["templates.empty"]    = "Empty canvas",

            // Cloud services gallery
            ["cloud.title"]    = "Cloud services",
            ["cloud.subtitle"] = "Click a service to drop it on the canvas.",

            ["template.blank"]              = "Blank canvas",
            ["template.blank.desc"]         = "Start from scratch with an empty diagram.",
            ["template.orgchart"]           = "Org chart",
            ["template.orgchart.desc"]      = "Simple three-level organization chart.",
            ["template.webarch"]            = "Web architecture",
            ["template.webarch.desc"]       = "Client → load balancer → app servers → database with cache.",
            ["template.clientserver"]       = "Client-server",
            ["template.clientserver.desc"]  = "Web/mobile clients calling an API with a database.",
            ["template.microservices"]      = "Microservices",
            ["template.microservices.desc"] = "API gateway fanning out to several services with a queue.",
            ["template.datapipeline"]       = "Data pipeline",
            ["template.datapipeline.desc"]  = "Source → ingestion → processing → warehouse → dashboard.",
            ["template.restapi"]            = "REST API",
            ["template.restapi.desc"]       = "Client → API gateway → API servers with a cache and database.",
            ["template.serverless"]         = "Serverless (AWS)",
            ["template.serverless.desc"]    = "CloudFront → API Gateway → Lambda → DynamoDB and S3.",
            ["template.threetier"]          = "Three-tier web app",
            ["template.threetier.desc"]     = "Browser → web server → app server → database.",
            ["template.kubernetes"]         = "Kubernetes cluster",
            ["template.kubernetes.desc"]    = "Ingress routing to service deployments backed by a database.",
            ["template.cicd"]               = "CI/CD pipeline",
            ["template.cicd.desc"]          = "Commit → build → test → deploy to production, with an artifact registry.",
            ["template.eventdriven"]        = "Event-driven",
            ["template.eventdriven.desc"]   = "Producer → message queue → consumers writing to data stores.",
            ["template.frontendbackend"]      = "Frontend & Backend",
            ["template.frontendbackend.desc"] = "SPA frontend → backend API with auth middleware, cache, and database.",
            ["template.middleware"]           = "Message middleware",
            ["template.middleware.desc"]      = "Apps publish to a message broker; consumer services write to data stores.",
            ["template.dbcluster"]            = "Database cluster",
            ["template.dbcluster.desc"]       = "App servers → primary database with read replicas and a backup.",
            ["template.fullstack"]            = "Full-stack web",
            ["template.fullstack.desc"]       = "Browser / mobile → CDN & web frontend → API gateway → backend with cache and database.",
            ["template.caching"]              = "Caching layer",
            ["template.caching.desc"]         = "Client → app server → cache-aside in front of the database.",

            // Help dialog
            ["help.title"]   = "Keyboard shortcuts",
            ["help.tools"]   = "TOOLS",
            ["help.editing"] = "EDITING",
            ["help.file"]    = "FILE",
            ["help.view"]    = "VIEW",
            ["help.close"]   = "Close",
            ["help.action.select"]     = "Select tool",
            ["help.action.connector"]  = "Connector tool",
            ["help.action.rectangle"]  = "Rectangle",
            ["help.action.rounded"]    = "Rounded / Component",
            ["help.action.ellipse"]    = "Ellipse",
            ["help.action.diamond"]    = "Diamond / Decision",
            ["help.action.hexagon"]    = "Hexagon",
            ["help.action.database"]   = "Database (cylinder)",
            ["help.action.cloud"]      = "Cloud",
            ["help.action.server"]     = "Server",
            ["help.action.user"]       = "User / Person",
            ["help.action.text"]       = "Text label",
            ["help.action.cancel"]     = "Cancel / select",
            ["help.action.undo"]       = "Undo",
            ["help.action.redo"]       = "Redo",
            ["help.action.copy"]       = "Copy",
            ["help.action.cut"]        = "Cut",
            ["help.action.paste"]      = "Paste",
            ["help.action.duplicate"]  = "Duplicate",
            ["help.action.selectall"]  = "Select all",
            ["help.action.delete"]     = "Delete selection",
            ["help.action.edit"]       = "Edit label",
            ["help.action.new"]        = "New diagram",
            ["help.action.open"]       = "Open",
            ["help.action.save"]       = "Save",
            ["help.action.export"]     = "Export PNG",
            ["help.action.pan.space"]  = "Pan canvas",
            ["help.action.pan.right"]  = "Pan canvas",
            ["help.action.zoom"]       = "Zoom in / out",
            ["help.action.scroll"]     = "Scroll (Shift = sideways)",
            ["help.action.doubleclick"]= "Double-click",

            // Menu
            ["menu.file"]            = "_File",
            ["menu.file.new"]        = "_New",
            ["menu.file.templates"]  = "_Templates...",
            ["menu.file.cloud"]      = "_Cloud Services...",
            ["menu.file.open"]       = "_Open...",
            ["menu.file.recent"]        = "Open _Recent",
            ["menu.file.recent.none"]   = "(no recent files)",
            ["menu.file.recent.clear"]  = "Clear recent",
            ["menu.file.import.excalidraw"] = "_Import from Excalidraw...",
            ["menu.file.save"]       = "_Save...",
            ["menu.file.export"]     = "_Export PNG...",
            ["menu.file.export.excalidraw"] = "Export to _Excalidraw...",
            ["menu.file.export.drawio"]     = "Export to _draw.io...",
            ["menu.file.export.mermaid"]    = "Export to _Mermaid...",
            ["menu.file.exit"]       = "E_xit",
            ["menu.edit"]            = "_Edit",
            ["menu.edit.undo"]       = "_Undo",
            ["menu.edit.redo"]       = "_Redo",
            ["menu.edit.cut"]        = "Cu_t",
            ["menu.edit.copy"]       = "_Copy",
            ["menu.edit.paste"]      = "_Paste",
            ["menu.edit.duplicate"]  = "_Duplicate",
            ["menu.edit.delete"]     = "De_lete",
            ["menu.edit.selectall"]  = "Select _All",
            ["menu.edit.insertimage"]= "Insert _Image...",
            ["menu.edit.preferences"]= "_Preferences...",
            ["menu.view"]            = "_View",
            ["menu.view.zoomin"]     = "Zoom _In",
            ["menu.view.zoomout"]    = "Zoom _Out",
            ["menu.view.zoomreset"]  = "_Reset Zoom",
            ["menu.view.clearguides"]= "Clear _Guides",
            ["menu.lang"]            = "_Language",
            ["menu.lang.en"]         = "English",
            ["menu.lang.es"]         = "Español",
            ["menu.help"]            = "_Help",
            ["menu.help.manual"]     = "_User Manual...",
            ["menu.help.shortcuts"]  = "_Keyboard Shortcuts...",

            // Modal
            ["modal.ok"]              = "OK",
            ["modal.cancel"]          = "Cancel",
            ["modal.unsaved.title"]   = "Unsaved changes",
            ["modal.unsaved.body"]    = "You have unsaved changes. Do you want to save before closing?",
            ["modal.unsaved.save"]    = "Save",
            ["modal.unsaved.discard"] = "Don't save",
            ["modal.new.title"]       = "New diagram?",
            ["modal.new.body"]        = "This will clear the current canvas. You'll lose any unsaved changes.",
            ["modal.new.confirm"]     = "Start new",
            ["modal.saved.title"]     = "Saved",
            ["modal.saved.body"]      = "Diagram saved to {0}.",
            ["modal.savefail.title"]  = "Could not save",
            ["modal.openfail.title"]  = "Could not open file",
            ["modal.exported.title"]  = "Exported",
            ["modal.exported.body"]   = "PNG written to {0}.",
            ["modal.exportfail.title"]= "Could not export",

            // Color picker
            ["color.title"]    = "Color picker",
            ["color.hex"]      = "Hex",
            ["color.preview"]  = "Preview",
            ["color.standard"] = "STANDARD COLORS",
            ["color.custom"]   = "Custom color...",
            ["color.apply"]    = "Apply",

            // Right-click context menu
            ["ctx.edittext"]  = "Edit text",
            ["ctx.fill"]      = "Fill",
            ["ctx.stroke"]    = "Stroke",
            ["ctx.front"]     = "Bring to front",
            ["ctx.back"]      = "Send to back",
            ["ctx.duplicate"] = "Duplicate",
            ["ctx.copy"]      = "Copy",
            ["ctx.delete"]    = "Delete",

            // Connector context menu
            ["conn.routing"]  = "Routing",
            ["conn.straight"] = "Straight",
            ["conn.curved"]   = "Curved",
            ["conn.elbow"]    = "Elbow",
            ["conn.stroke"]   = "Stroke",
            ["conn.solid"]    = "Solid",
            ["conn.dashed"]   = "Dashed",
            ["conn.dotted"]   = "Dotted",

            // Preferences
            ["pref.title"]     = "Preferences",
            ["pref.connector"] = "Default connector",
            ["pref.snap"]      = "Snap to objects and guides",
            ["pref.units"]     = "Ruler units",
            ["unit.pixels"]    = "Pixels",
            ["unit.cm"]        = "Centimeters",
            ["unit.inches"]    = "Inches",
            ["unit.picas"]     = "Picas",

            // User manual
            ["manual.title"]    = "User manual",
            ["manual.subtitle"] = "Everything you need to build diagrams fast",

            ["manual.overview.h"] = "Overview",
            ["manual.overview.p"] = "DrawThisEasy helps you sketch org charts, architecture diagrams and system flows in seconds. The idea is simple: pick a tool, click the canvas to drop a shape, type a label, then drag from one shape to another to connect them.",

            ["manual.workspace.h"]          = "The workspace",
            ["manual.workspace.p"]          = "The window is organized into a few clear areas:",
            ["manual.workspace.b.strip"]    = "Top tool strip — one-click icon buttons for every tool, next to the menu bar and zoom controls.",
            ["manual.workspace.b.palette"]  = "Left palette — the same tools grouped into Tools, Shapes and Infrastructure, with labels.",
            ["manual.workspace.b.canvas"]   = "Canvas — the dotted grid in the center where you build the diagram.",
            ["manual.workspace.b.inspector"]= "Inspector — appears on the right when a shape is selected; sets fill, stroke and layer order.",
            ["manual.workspace.b.status"]   = "Status bar — shows the active tool and a short hint about what to do next.",

            ["manual.shapes.h"]  = "Adding shapes",
            ["manual.shapes.p"]  = "Click a shape tool in the palette or top strip (or press its keyboard shortcut), then click the canvas to drop the shape. The tool stays active so you can keep stamping shapes; click an existing object — or the Select tool (V) — to go back to selecting.",
            ["manual.shapes.p2"] = "Flow shapes include Process, Component, Start / End, Decision, Hexagon and Data. Infrastructure shapes include Database, Cloud, Server, User, Queue, sticky Note and plain Text.",
            ["manual.shapes.p3"] = "You can also insert raster images (the toolbar Image button, the palette, or Edit ▸ Insert Image), and drop in cloud-provider service objects from the AWS / Azure / Google buttons in the toolbar and palette.",

            ["manual.connect.h"]  = "Connecting shapes",
            ["manual.connect.p"]  = "Switch to the Connector tool (press L), then drag from one shape to another. The connector snaps to the edge of each shape and automatically reroutes whenever you move either shape.",
            ["manual.connect.p2"] = "Connector mode is sticky — you can wire shape to shape to shape without re-selecting the tool. A single click on a shape (with no drag) simply selects it instead of starting a connection.",

            ["manual.select.h"]        = "Selecting, moving and resizing",
            ["manual.select.b.click"]  = "Click a shape to select it, then drag it to move. The Inspector appears for the selection.",
            ["manual.select.b.multi"]  = "Shift-click or Ctrl-click to add or remove shapes from the selection.",
            ["manual.select.b.marquee"]= "Hold Shift and drag across empty space to draw a marquee and select everything it touches.",
            ["manual.select.b.pan"]    = "Drag empty space (or hold Space, or right-click and drag) to pan the canvas instead.",
            ["manual.select.b.resize"] = "With a single shape selected, drag the square handles around it to resize.",

            ["manual.labels.h"] = "Editing labels",
            ["manual.labels.p"] = "Double-click any shape to edit its label in place. Type your text and press Enter to commit, or Esc to cancel. Connectors can carry labels too.",

            ["manual.colors.h"] = "Colors and style",
            ["manual.colors.p"] = "Select one or more shapes to reveal the Inspector, then click a Fill or Stroke swatch to apply it. Click the “+” swatch to choose a custom color from the picker. The built-in palette uses professional slate, sky, teal and amber tones.",

            ["manual.layers.h"] = "Layering",
            ["manual.layers.p"] = "When shapes overlap, use Front and Back in the Inspector (or the Edit menu) to control which one sits on top.",

            ["manual.view.h"]       = "Panning and zooming",
            ["manual.view.b.pan"]   = "Pan / scroll: the mouse wheel scrolls vertically (Shift for sideways), or drag an empty area, hold Space and drag, right-click and drag, or use the scrollbars.",
            ["manual.view.b.zoom"]  = "Zoom: hold Ctrl and scroll the mouse wheel, or use the zoom buttons in the top bar.",
            ["manual.view.b.reset"] = "Reset zoom returns the view to 100%.",

            ["manual.templates.h"] = "Templates",
            ["manual.templates.p"] = "Start fast from a ready-made template — Org chart, Web architecture, Client-server, Microservices, Data pipeline, or a blank canvas. Open Templates from the toolbar or the File menu; everything stays fully editable afterwards.",

            ["manual.files.h"]        = "Saving, opening and exporting",
            ["manual.files.b.save"]   = "Save and Open store diagrams as .ptd.json files you can reopen later; Open also takes several files at once.",
            ["manual.files.b.recent"] = "Open Recent (File menu) lists your last five files for one-click reopening.",
            ["manual.files.b.export"] = "Export PNG writes a crisp 2× resolution image; Export to Excalidraw, draw.io, or Mermaid sends your diagram to other tools.",
            ["manual.files.b.import"] = "Open or import .excalidraw scenes (shapes, labels, images, and arrows come across).",
            ["manual.files.b.new"]    = "New (Ctrl+N, or the + tab) opens another diagram in its own tab; switch between open diagrams along the top.",

            ["manual.editing.h"] = "Copy, paste and undo",
            ["manual.editing.p"] = "Copy, Cut and Paste (Ctrl+C / X / V) work on a selection and even between two open DrawThisEasy windows, preserving the connections between shapes. Duplicate with Ctrl+D, select everything with Ctrl+A, and step backward or forward with Undo (Ctrl+Z) and Redo (Ctrl+Y).",

            ["manual.language.h"] = "Language",
            ["manual.language.p"] = "Switch between English and Español at any time from the Language menu or the EN / ES button in the top bar. The whole interface updates live — no restart required.",

            ["manual.shortcuts.h"] = "Keyboard shortcuts",
            ["manual.shortcuts.p"] = "Prefer the keyboard? Open Help ▸ Keyboard Shortcuts (or press ?) for the full list of shortcuts. Press F1 anytime to reopen this manual.",
        },

        [Language.Es] = new()
        {
            // Top bar
            ["topbar.new"]       = "Nuevo",
            ["topbar.templates"] = "Plantillas",
            ["topbar.cloud"]     = "Nube",
            ["topbar.image"]     = "Imagen",
            ["topbar.templates.tip"] = "Explorar plantillas",
            ["topbar.cloud.tip"]     = "Servicios en la nube",
            ["topbar.image.tip"]     = "Insertar imagen",
            ["topbar.open"]      = "Abrir",
            ["topbar.save"]      = "Guardar",
            ["topbar.export"]    = "Exportar",
            ["topbar.help.tip"]  = "Atajos de teclado",
            ["topbar.lang.tip"]  = "Idioma",
            ["topbar.zoom.in"]   = "Acercar",
            ["topbar.zoom.out"]  = "Alejar",
            ["topbar.zoom.reset"]= "Restablecer zoom",

            // Palette groups
            ["group.tools"]  = "Herramientas",
            ["group.shapes"] = "Formas",
            ["group.infra"]  = "Infraestructura",
            ["group.cloud"]  = "Nube",

            // Tool labels
            ["tool.select"]    = "Seleccionar",
            ["tool.connect"]   = "Conector",
            ["tool.pan"]       = "Desplazar",
            ["tool.process"]   = "Proceso",
            ["tool.component"] = "Componente",
            ["tool.startend"]  = "Inicio / Fin",
            ["tool.decision"]  = "Decisión",
            ["tool.hexagon"]   = "Hexágono",
            ["tool.data"]      = "Datos",
            ["tool.database"]  = "Base de datos",
            ["tool.cloud"]     = "Nube",
            ["tool.server"]    = "Servidor",
            ["tool.user"]      = "Usuario",
            ["tool.queue"]     = "Cola",
            ["tool.note"]      = "Nota",
            ["tool.text"]      = "Texto",

            // Tooltips
            ["tip.select"]        = "Seleccionar y mover (V)",
            ["tip.connect"]       = "Conectar formas (L)",
            ["tip.pan"]           = "Desplazar el lienzo (Espacio + arrastrar)",
            ["tip.rectangle"]     = "Rectángulo / Proceso (R)",
            ["tip.rounded"]       = "Redondeado / Componente (O)",
            ["tip.ellipse"]       = "Elipse / Inicio-Fin (E)",
            ["tip.diamond"]       = "Decisión (D)",
            ["tip.hexagon"]       = "Hexágono (H)",
            ["tip.parallelogram"] = "Paralelogramo / Datos",
            ["tip.cylinder"]      = "Base de datos (B)",
            ["tip.cloud"]         = "Servicio en la nube (C)",
            ["tip.server"]        = "Servidor (S)",
            ["tip.person"]        = "Usuario / Persona (P)",
            ["tip.queue"]         = "Cola / flujo",
            ["tip.note"]          = "Nota adhesiva",
            ["tip.text"]          = "Etiqueta de texto (T)",

            ["palette.hint"] = "Elige una herramienta y haz clic en el lienzo.\nEn modo Conector, arrastra desde el borde de una forma hasta otra para enlazarlas.",

            // Status
            ["status.add.prefix"]   = "Añadir ",
            ["status.hint.select"]  = "Haz clic para seleccionar. Arrastra para mover. Doble clic para editar texto. Espacio + arrastrar para desplazar.",
            ["status.hint.connect"] = "Haz clic en una forma y arrástrala hasta otra para conectarlas.",
            ["status.hint.pan"]     = "Arrastra el lienzo para desplazarlo.",
            ["status.hint.add"]     = "Haz clic en el lienzo para colocar la forma.",

            // Default labels
            ["default.process"]   = "Proceso",
            ["default.component"] = "Componente",
            ["default.start"]     = "Inicio",
            ["default.decision"]  = "Decisión",
            ["default.step"]      = "Paso",
            ["default.data"]      = "Datos",
            ["default.database"]  = "Base de datos",
            ["default.cloud"]     = "Servicio Nube",
            ["default.server"]    = "Servidor",
            ["default.user"]      = "Usuario",
            ["default.queue"]     = "Cola",
            ["default.note"]      = "Nota",
            ["default.text"]      = "Texto",

            // Inspector
            ["inspector.fill"]   = "RELLENO",
            ["inspector.stroke"] = "BORDE",
            ["inspector.front"]  = "Frente",
            ["inspector.back"]   = "Atrás",
            ["inspector.dup"]    = "Dupl.",
            ["inspector.delete"] = "Eliminar",
            ["inspector.tip.front"]  = "Traer al frente",
            ["inspector.tip.back"]   = "Enviar al fondo",
            ["inspector.tip.dup"]    = "Duplicar (Ctrl+D)",
            ["inspector.tip.delete"] = "Eliminar (Supr)",

            // Templates dialog
            ["templates.title"]    = "Comenzar desde una plantilla",
            ["templates.subtitle"] = "Elige un punto de partida. Después puedes editarlo todo.",
            ["templates.cancel"]   = "Cancelar",
            ["templates.empty"]    = "Lienzo vacío",

            // Cloud services gallery
            ["cloud.title"]    = "Servicios en la nube",
            ["cloud.subtitle"] = "Haz clic en un servicio para colocarlo en el lienzo.",

            ["template.blank"]              = "Lienzo en blanco",
            ["template.blank.desc"]         = "Empieza desde cero con un diagrama vacío.",
            ["template.orgchart"]           = "Organigrama",
            ["template.orgchart.desc"]      = "Organigrama sencillo de tres niveles.",
            ["template.webarch"]            = "Arquitectura web",
            ["template.webarch.desc"]       = "Cliente → balanceador → servidores → base de datos con caché.",
            ["template.clientserver"]       = "Cliente-servidor",
            ["template.clientserver.desc"]  = "Clientes web/móvil llamando a una API con base de datos.",
            ["template.microservices"]      = "Microservicios",
            ["template.microservices.desc"] = "Pasarela API distribuyendo a varios servicios con una cola.",
            ["template.datapipeline"]       = "Tubería de datos",
            ["template.datapipeline.desc"]  = "Origen → ingesta → procesamiento → almacén → tableros.",
            ["template.restapi"]            = "API REST",
            ["template.restapi.desc"]       = "Cliente → pasarela API → servidores API con caché y base de datos.",
            ["template.serverless"]         = "Serverless (AWS)",
            ["template.serverless.desc"]    = "CloudFront → API Gateway → Lambda → DynamoDB y S3.",
            ["template.threetier"]          = "App web de tres capas",
            ["template.threetier.desc"]     = "Navegador → servidor web → servidor de aplicaciones → base de datos.",
            ["template.kubernetes"]         = "Clúster de Kubernetes",
            ["template.kubernetes.desc"]    = "Ingress que enruta a despliegues de servicios con una base de datos.",
            ["template.cicd"]               = "Tubería CI/CD",
            ["template.cicd.desc"]          = "Commit → compilar → probar → desplegar a producción, con registro de artefactos.",
            ["template.eventdriven"]        = "Orientado a eventos",
            ["template.eventdriven.desc"]   = "Productor → cola de mensajes → consumidores que escriben en almacenes.",
            ["template.frontendbackend"]      = "Frontend y Backend",
            ["template.frontendbackend.desc"] = "Frontend SPA → API backend con middleware de autenticación, caché y base de datos.",
            ["template.middleware"]           = "Middleware de mensajería",
            ["template.middleware.desc"]      = "Las apps publican en un broker de mensajes; los servicios consumidores escriben en almacenes.",
            ["template.dbcluster"]            = "Clúster de base de datos",
            ["template.dbcluster.desc"]       = "Servidores de aplicación → base de datos principal con réplicas de lectura y copia de seguridad.",
            ["template.fullstack"]            = "Web full-stack",
            ["template.fullstack.desc"]       = "Navegador / móvil → CDN y frontend web → pasarela API → backend con caché y base de datos.",
            ["template.caching"]              = "Capa de caché",
            ["template.caching.desc"]         = "Cliente → servidor de aplicación → caché (cache-aside) frente a la base de datos.",

            // Help dialog
            ["help.title"]   = "Atajos de teclado",
            ["help.tools"]   = "HERRAMIENTAS",
            ["help.editing"] = "EDICIÓN",
            ["help.file"]    = "ARCHIVO",
            ["help.view"]    = "VISTA",
            ["help.close"]   = "Cerrar",
            ["help.action.select"]     = "Herramienta seleccionar",
            ["help.action.connector"]  = "Herramienta conector",
            ["help.action.rectangle"]  = "Rectángulo",
            ["help.action.rounded"]    = "Redondeado / Componente",
            ["help.action.ellipse"]    = "Elipse",
            ["help.action.diamond"]    = "Rombo / Decisión",
            ["help.action.hexagon"]    = "Hexágono",
            ["help.action.database"]   = "Base de datos (cilindro)",
            ["help.action.cloud"]      = "Nube",
            ["help.action.server"]     = "Servidor",
            ["help.action.user"]       = "Usuario / Persona",
            ["help.action.text"]       = "Etiqueta de texto",
            ["help.action.cancel"]     = "Cancelar / seleccionar",
            ["help.action.undo"]       = "Deshacer",
            ["help.action.redo"]       = "Rehacer",
            ["help.action.copy"]       = "Copiar",
            ["help.action.cut"]        = "Cortar",
            ["help.action.paste"]      = "Pegar",
            ["help.action.duplicate"]  = "Duplicar",
            ["help.action.selectall"]  = "Seleccionar todo",
            ["help.action.delete"]     = "Eliminar selección",
            ["help.action.edit"]       = "Editar etiqueta",
            ["help.action.new"]        = "Nuevo diagrama",
            ["help.action.open"]       = "Abrir",
            ["help.action.save"]       = "Guardar",
            ["help.action.export"]     = "Exportar PNG",
            ["help.action.pan.space"]  = "Desplazar lienzo",
            ["help.action.pan.right"]  = "Desplazar lienzo",
            ["help.action.zoom"]       = "Acercar / alejar",
            ["help.action.scroll"]     = "Desplazar (Mayús = lateral)",
            ["help.action.doubleclick"]= "Doble clic",

            // Menu
            ["menu.file"]            = "_Archivo",
            ["menu.file.new"]        = "_Nuevo",
            ["menu.file.templates"]  = "_Plantillas...",
            ["menu.file.cloud"]      = "Servicios en la _nube...",
            ["menu.file.open"]       = "_Abrir...",
            ["menu.file.recent"]        = "Abrir _recientes",
            ["menu.file.recent.none"]   = "(sin archivos recientes)",
            ["menu.file.recent.clear"]  = "Borrar recientes",
            ["menu.file.import.excalidraw"] = "_Importar de Excalidraw...",
            ["menu.file.save"]       = "_Guardar...",
            ["menu.file.export"]     = "E_xportar PNG...",
            ["menu.file.export.excalidraw"] = "Exportar a _Excalidraw...",
            ["menu.file.export.drawio"]     = "Exportar a _draw.io...",
            ["menu.file.export.mermaid"]    = "Exportar a _Mermaid...",
            ["menu.file.exit"]       = "_Salir",
            ["menu.edit"]            = "_Edición",
            ["menu.edit.undo"]       = "_Deshacer",
            ["menu.edit.redo"]       = "_Rehacer",
            ["menu.edit.cut"]        = "Cor_tar",
            ["menu.edit.copy"]       = "_Copiar",
            ["menu.edit.paste"]      = "_Pegar",
            ["menu.edit.duplicate"]  = "_Duplicar",
            ["menu.edit.delete"]     = "E_liminar",
            ["menu.edit.selectall"]  = "Seleccionar _todo",
            ["menu.edit.insertimage"]= "Insertar _imagen...",
            ["menu.edit.preferences"]= "_Preferencias...",
            ["menu.view"]            = "_Vista",
            ["menu.view.zoomin"]     = "_Acercar",
            ["menu.view.zoomout"]    = "Ale_jar",
            ["menu.view.zoomreset"]  = "_Restablecer zoom",
            ["menu.view.clearguides"]= "Borrar _guías",
            ["menu.lang"]            = "_Idioma",
            ["menu.lang.en"]         = "English",
            ["menu.lang.es"]         = "Español",
            ["menu.help"]            = "A_yuda",
            ["menu.help.manual"]     = "_Manual de usuario...",
            ["menu.help.shortcuts"]  = "_Atajos de teclado...",

            // Modal
            ["modal.ok"]              = "Aceptar",
            ["modal.cancel"]          = "Cancelar",
            ["modal.unsaved.title"]   = "Cambios sin guardar",
            ["modal.unsaved.body"]    = "Tienes cambios sin guardar. ¿Quieres guardarlos antes de cerrar?",
            ["modal.unsaved.save"]    = "Guardar",
            ["modal.unsaved.discard"] = "No guardar",
            ["modal.new.title"]       = "¿Nuevo diagrama?",
            ["modal.new.body"]        = "Esto borrará el lienzo actual. Perderás los cambios no guardados.",
            ["modal.new.confirm"]     = "Comenzar",
            ["modal.saved.title"]     = "Guardado",
            ["modal.saved.body"]      = "Diagrama guardado en {0}.",
            ["modal.savefail.title"]  = "No se pudo guardar",
            ["modal.openfail.title"]  = "No se pudo abrir el archivo",
            ["modal.exported.title"]  = "Exportado",
            ["modal.exported.body"]   = "PNG escrito en {0}.",
            ["modal.exportfail.title"]= "No se pudo exportar",

            // Color picker
            ["color.title"]    = "Selector de color",
            ["color.hex"]      = "Hex",
            ["color.preview"]  = "Vista previa",
            ["color.standard"] = "COLORES ESTÁNDAR",
            ["color.custom"]   = "Color personalizado...",
            ["color.apply"]    = "Aplicar",

            // Right-click context menu
            ["ctx.edittext"]  = "Editar texto",
            ["ctx.fill"]      = "Relleno",
            ["ctx.stroke"]    = "Borde",
            ["ctx.front"]     = "Traer al frente",
            ["ctx.back"]      = "Enviar al fondo",
            ["ctx.duplicate"] = "Duplicar",
            ["ctx.copy"]      = "Copiar",
            ["ctx.delete"]    = "Eliminar",

            // Connector context menu
            ["conn.routing"]  = "Trazado",
            ["conn.straight"] = "Recta",
            ["conn.curved"]   = "Curva",
            ["conn.elbow"]    = "En codo",
            ["conn.stroke"]   = "Línea",
            ["conn.solid"]    = "Sólida",
            ["conn.dashed"]   = "Discontinua",
            ["conn.dotted"]   = "Punteada",

            // Preferences
            ["pref.title"]     = "Preferencias",
            ["pref.connector"] = "Conector predeterminado",
            ["pref.snap"]      = "Ajustar a objetos y guías",
            ["pref.units"]     = "Unidades de regla",
            ["unit.pixels"]    = "Píxeles",
            ["unit.cm"]        = "Centímetros",
            ["unit.inches"]    = "Pulgadas",
            ["unit.picas"]     = "Picas",

            // User manual
            ["manual.title"]    = "Manual de usuario",
            ["manual.subtitle"] = "Todo lo que necesitas para crear diagramas rápido",

            ["manual.overview.h"] = "Descripción general",
            ["manual.overview.p"] = "DrawThisEasy te ayuda a esbozar organigramas, diagramas de arquitectura y flujos de sistemas en segundos. La idea es sencilla: elige una herramienta, haz clic en el lienzo para colocar una forma, escribe una etiqueta y arrastra de una forma a otra para conectarlas.",

            ["manual.workspace.h"]          = "El área de trabajo",
            ["manual.workspace.p"]          = "La ventana se organiza en unas pocas zonas claras:",
            ["manual.workspace.b.strip"]    = "Barra de herramientas superior: botones de un clic para cada herramienta, junto al menú y los controles de zoom.",
            ["manual.workspace.b.palette"]  = "Paleta izquierda: las mismas herramientas agrupadas en Herramientas, Formas e Infraestructura, con etiquetas.",
            ["manual.workspace.b.canvas"]   = "Lienzo: la cuadrícula de puntos en el centro donde construyes el diagrama.",
            ["manual.workspace.b.inspector"]= "Inspector: aparece a la derecha cuando hay una forma seleccionada; ajusta relleno, borde y orden de capas.",
            ["manual.workspace.b.status"]   = "Barra de estado: muestra la herramienta activa y una breve sugerencia de qué hacer.",

            ["manual.shapes.h"]  = "Agregar formas",
            ["manual.shapes.p"]  = "Haz clic en una herramienta de forma en la paleta o en la barra superior (o pulsa su atajo) y luego haz clic en el lienzo para colocar la forma. La herramienta permanece activa para seguir colocando formas; haz clic en un objeto existente — o en la herramienta Seleccionar (V) — para volver a seleccionar.",
            ["manual.shapes.p2"] = "Las formas de flujo incluyen Proceso, Componente, Inicio / Fin, Decisión, Hexágono y Datos. Las de infraestructura incluyen Base de datos, Nube, Servidor, Usuario, Cola, Nota adhesiva y Texto simple.",
            ["manual.shapes.p3"] = "También puedes insertar imágenes (el botón Imagen de la barra, la paleta, o Edición ▸ Insertar imagen) y agregar objetos de servicios en la nube con los botones AWS / Azure / Google de la barra y la paleta.",

            ["manual.connect.h"]  = "Conectar formas",
            ["manual.connect.p"]  = "Cambia a la herramienta Conector (pulsa L) y arrastra de una forma a otra. El conector se ajusta al borde de cada forma y se redibuja automáticamente cada vez que mueves cualquiera de ellas.",
            ["manual.connect.p2"] = "El modo Conector es continuo: puedes enlazar forma con forma con forma sin volver a elegir la herramienta. Un solo clic en una forma (sin arrastrar) simplemente la selecciona en lugar de iniciar una conexión.",

            ["manual.select.h"]        = "Seleccionar, mover y redimensionar",
            ["manual.select.b.click"]  = "Haz clic en una forma para seleccionarla y arrástrala para moverla. El Inspector aparece para la selección.",
            ["manual.select.b.multi"]  = "Mantén Mayús o Ctrl y haz clic para agregar o quitar formas de la selección.",
            ["manual.select.b.marquee"]= "Mantén Mayús y arrastra sobre un área vacía para dibujar un marco y seleccionar todo lo que toque.",
            ["manual.select.b.pan"]    = "Arrastra un área vacía (o mantén Espacio, o haz clic derecho y arrastra) para desplazar el lienzo.",
            ["manual.select.b.resize"] = "Con una sola forma seleccionada, arrastra los tiradores cuadrados que la rodean para redimensionarla.",

            ["manual.labels.h"] = "Editar etiquetas",
            ["manual.labels.p"] = "Haz doble clic en cualquier forma para editar su etiqueta en el lugar. Escribe el texto y pulsa Entrar para confirmar, o Esc para cancelar. Los conectores también pueden llevar etiquetas.",

            ["manual.colors.h"] = "Colores y estilo",
            ["manual.colors.p"] = "Selecciona una o varias formas para mostrar el Inspector y luego haz clic en una muestra de Relleno o Borde para aplicarla. Haz clic en la muestra «+» para elegir un color personalizado con el selector. La paleta integrada usa tonos profesionales de pizarra, cielo, verde azulado y ámbar.",

            ["manual.layers.h"] = "Capas",
            ["manual.layers.p"] = "Cuando las formas se superponen, usa Frente y Atrás en el Inspector (o el menú Edición) para controlar cuál queda encima.",

            ["manual.view.h"]       = "Desplazar y hacer zoom",
            ["manual.view.b.pan"]   = "Desplazar: la rueda del ratón desplaza verticalmente (Mayús para lateral), o arrastra un área vacía, mantén Espacio y arrastra, haz clic derecho y arrastra, o usa las barras de desplazamiento.",
            ["manual.view.b.zoom"]  = "Zoom: mantén Ctrl y gira la rueda del ratón, o usa los botones de zoom de la barra superior.",
            ["manual.view.b.reset"] = "Restablecer zoom devuelve la vista al 100%.",

            ["manual.templates.h"] = "Plantillas",
            ["manual.templates.p"] = "Empieza rápido con una plantilla lista: Organigrama, Arquitectura web, Cliente-servidor, Microservicios, Tubería de datos o un lienzo en blanco. Abre Plantillas desde la barra de herramientas o el menú Archivo; después puedes editarlo todo.",

            ["manual.files.h"]        = "Guardar, abrir y exportar",
            ["manual.files.b.save"]   = "Guardar y Abrir almacenan los diagramas como archivos .ptd.json que puedes reabrir después; Abrir también acepta varios archivos a la vez.",
            ["manual.files.b.recent"] = "Abrir recientes (menú Archivo) muestra tus últimos cinco archivos para reabrirlos con un clic.",
            ["manual.files.b.export"] = "Exportar PNG genera una imagen nítida al doble de resolución (2×); Exportar a Excalidraw, draw.io o Mermaid envía tu diagrama a otras herramientas.",
            ["manual.files.b.import"] = "Abre o importa escenas .excalidraw (formas, etiquetas, imágenes y flechas se conservan).",
            ["manual.files.b.new"]    = "Nuevo (Ctrl+N, o la pestaña +) abre otro diagrama en su propia pestaña; cambia entre los diagramas abiertos en la parte superior.",

            ["manual.editing.h"] = "Copiar, pegar y deshacer",
            ["manual.editing.p"] = "Copiar, Cortar y Pegar (Ctrl+C / X / V) funcionan sobre una selección e incluso entre dos ventanas abiertas de DrawThisEasy, conservando las conexiones entre formas. Duplica con Ctrl+D, selecciona todo con Ctrl+A y avanza o retrocede con Deshacer (Ctrl+Z) y Rehacer (Ctrl+Y).",

            ["manual.language.h"] = "Idioma",
            ["manual.language.p"] = "Cambia entre English y Español en cualquier momento desde el menú Idioma o el botón EN / ES de la barra superior. Toda la interfaz se actualiza al instante, sin reiniciar.",

            ["manual.shortcuts.h"] = "Atajos de teclado",
            ["manual.shortcuts.p"] = "¿Prefieres el teclado? Abre Ayuda ▸ Atajos de teclado (o pulsa ?) para ver la lista completa de atajos. Pulsa F1 en cualquier momento para volver a abrir este manual.",
        }
    };
}
