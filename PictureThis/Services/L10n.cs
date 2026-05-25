using System;
using System.Collections.Generic;

namespace PictureThis.Services;

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
            ["help.action.doubleclick"]= "Double-click",

            // Menu
            ["menu.file"]            = "_File",
            ["menu.file.new"]        = "_New",
            ["menu.file.templates"]  = "_Templates...",
            ["menu.file.open"]       = "_Open...",
            ["menu.file.save"]       = "_Save...",
            ["menu.file.export"]     = "_Export PNG...",
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
            ["menu.view"]            = "_View",
            ["menu.view.zoomin"]     = "Zoom _In",
            ["menu.view.zoomout"]    = "Zoom _Out",
            ["menu.view.zoomreset"]  = "_Reset Zoom",
            ["menu.lang"]            = "_Language",
            ["menu.lang.en"]         = "English",
            ["menu.lang.es"]         = "Español",
            ["menu.help"]            = "_Help",
            ["menu.help.shortcuts"]  = "_Keyboard Shortcuts...",

            // Modal
            ["modal.ok"]              = "OK",
            ["modal.cancel"]          = "Cancel",
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
        },

        [Language.Es] = new()
        {
            // Top bar
            ["topbar.new"]       = "Nuevo",
            ["topbar.templates"] = "Plantillas",
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
            ["help.action.doubleclick"]= "Doble clic",

            // Menu
            ["menu.file"]            = "_Archivo",
            ["menu.file.new"]        = "_Nuevo",
            ["menu.file.templates"]  = "_Plantillas...",
            ["menu.file.open"]       = "_Abrir...",
            ["menu.file.save"]       = "_Guardar...",
            ["menu.file.export"]     = "E_xportar PNG...",
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
            ["menu.view"]            = "_Vista",
            ["menu.view.zoomin"]     = "_Acercar",
            ["menu.view.zoomout"]    = "Ale_jar",
            ["menu.view.zoomreset"]  = "_Restablecer zoom",
            ["menu.lang"]            = "_Idioma",
            ["menu.lang.en"]         = "English",
            ["menu.lang.es"]         = "Español",
            ["menu.help"]            = "A_yuda",
            ["menu.help.shortcuts"]  = "_Atajos de teclado...",

            // Modal
            ["modal.ok"]              = "Aceptar",
            ["modal.cancel"]          = "Cancelar",
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
        }
    };
}
