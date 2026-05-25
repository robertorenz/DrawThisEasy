using System.Collections.Generic;
using PictureThis.Models;

namespace PictureThis.Services;

public record DiagramTemplate(string Id, string TitleKey, string DescriptionKey, DiagramModel Builder)
{
    public string Title       => L10n.T(TitleKey);
    public string Description => L10n.T(DescriptionKey);
}

public static class Templates
{
    public static List<DiagramTemplate> All() => new()
    {
        new("blank",         "template.blank",         "template.blank.desc",         new DiagramModel { Title = "Untitled" }),
        new("org-chart",     "template.orgchart",      "template.orgchart.desc",      BuildOrgChart()),
        new("web-arch",      "template.webarch",       "template.webarch.desc",       BuildWebArchitecture()),
        new("client-server", "template.clientserver",  "template.clientserver.desc",  BuildClientServer()),
        new("microservices", "template.microservices", "template.microservices.desc", BuildMicroservices()),
        new("data-pipeline", "template.datapipeline",  "template.datapipeline.desc",  BuildDataPipeline()),
    };

    private static ShapeNode N(ShapeKind k, double x, double y, double w, double h, string label, string? fill = null)
    {
        var n = new ShapeNode { Kind = k, X = x, Y = y, Width = w, Height = h, Label = label };
        if (fill != null) n.Fill = fill;
        return n;
    }

    private static DiagramModel BuildOrgChart()
    {
        var m = new DiagramModel { Title = "Org chart" };
        var ceo  = N(ShapeKind.Rounded, 360, 60,  160, 60, "CEO", "#DBEAFE");
        var cto  = N(ShapeKind.Rounded, 160, 200, 160, 60, "CTO", "#CCFBF1");
        var cfo  = N(ShapeKind.Rounded, 360, 200, 160, 60, "CFO", "#FEF3C7");
        var coo  = N(ShapeKind.Rounded, 560, 200, 160, 60, "COO", "#FFE4E6");
        var eng1 = N(ShapeKind.Rounded, 40,  340, 140, 50, "Engineering");
        var eng2 = N(ShapeKind.Rounded, 200, 340, 140, 50, "Platform");
        var fin1 = N(ShapeKind.Rounded, 360, 340, 140, 50, "Accounting");
        var op1  = N(ShapeKind.Rounded, 520, 340, 140, 50, "Operations");
        var op2  = N(ShapeKind.Rounded, 680, 340, 140, 50, "Support");
        m.Shapes.AddRange(new[] { ceo, cto, cfo, coo, eng1, eng2, fin1, op1, op2 });
        Link(m, ceo, cto); Link(m, ceo, cfo); Link(m, ceo, coo);
        Link(m, cto, eng1); Link(m, cto, eng2);
        Link(m, cfo, fin1);
        Link(m, coo, op1); Link(m, coo, op2);
        Z(m);
        return m;
    }

    private static DiagramModel BuildWebArchitecture()
    {
        var m = new DiagramModel { Title = "Web architecture" };
        var user = N(ShapeKind.Person,  60, 200, 90, 110, "User");
        var lb   = N(ShapeKind.Hexagon, 220, 220, 150, 70, "Load Balancer", "#FEF3C7");
        var app1 = N(ShapeKind.Server,  430, 100, 110, 130, "App Server 1", "#DBEAFE");
        var app2 = N(ShapeKind.Server,  430, 270, 110, 130, "App Server 2", "#DBEAFE");
        var cache = N(ShapeKind.Cylinder, 610, 100, 120, 90, "Redis Cache", "#FFE4E6");
        var db   = N(ShapeKind.Cylinder, 610, 270, 130, 100, "PostgreSQL", "#CCFBF1");
        var cdn  = N(ShapeKind.Cloud,    220, 60,  150, 90, "CDN", "#E0F2FE");
        m.Shapes.AddRange(new[] { user, lb, app1, app2, cache, db, cdn });
        Link(m, user, cdn);
        Link(m, user, lb);
        Link(m, lb, app1); Link(m, lb, app2);
        Link(m, app1, cache); Link(m, app2, cache);
        Link(m, app1, db); Link(m, app2, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildClientServer()
    {
        var m = new DiagramModel { Title = "Client-server" };
        var web = N(ShapeKind.Rounded, 60, 100, 140, 70, "Web Client", "#DBEAFE");
        var mob = N(ShapeKind.Rounded, 60, 240, 140, 70, "Mobile Client", "#DBEAFE");
        var api = N(ShapeKind.Hexagon, 290, 170, 170, 80, "REST API", "#FEF3C7");
        var auth = N(ShapeKind.Rounded, 540, 80, 140, 60, "Auth Service");
        var db  = N(ShapeKind.Cylinder, 540, 220, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { web, mob, api, auth, db });
        Link(m, web, api); Link(m, mob, api);
        Link(m, api, auth); Link(m, api, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildMicroservices()
    {
        var m = new DiagramModel { Title = "Microservices" };
        var user = N(ShapeKind.Person, 40, 200, 90, 110, "User");
        var gw   = N(ShapeKind.Hexagon, 190, 220, 160, 70, "API Gateway", "#FEF3C7");
        var s1   = N(ShapeKind.Rounded, 420, 60,  150, 60, "Users Service", "#DBEAFE");
        var s2   = N(ShapeKind.Rounded, 420, 160, 150, 60, "Orders Service", "#DBEAFE");
        var s3   = N(ShapeKind.Rounded, 420, 260, 150, 60, "Billing Service", "#DBEAFE");
        var s4   = N(ShapeKind.Rounded, 420, 360, 150, 60, "Notifications", "#DBEAFE");
        var q    = N(ShapeKind.Queue,   620, 270, 160, 50, "Event Bus", "#FFE4E6");
        var db1  = N(ShapeKind.Cylinder, 620, 60,  110, 80, "Users DB", "#CCFBF1");
        var db2  = N(ShapeKind.Cylinder, 620, 160, 110, 80, "Orders DB", "#CCFBF1");
        m.Shapes.AddRange(new[] { user, gw, s1, s2, s3, s4, q, db1, db2 });
        Link(m, user, gw);
        Link(m, gw, s1); Link(m, gw, s2); Link(m, gw, s3); Link(m, gw, s4);
        Link(m, s1, db1); Link(m, s2, db2);
        Link(m, s2, q); Link(m, q, s3); Link(m, q, s4);
        Z(m);
        return m;
    }

    private static DiagramModel BuildDataPipeline()
    {
        var m = new DiagramModel { Title = "Data pipeline" };
        var src1 = N(ShapeKind.Rounded, 40, 80, 140, 60, "App Events", "#DBEAFE");
        var src2 = N(ShapeKind.Rounded, 40, 180, 140, 60, "CRM Export", "#DBEAFE");
        var src3 = N(ShapeKind.Rounded, 40, 280, 140, 60, "3rd-Party API", "#DBEAFE");
        var ing  = N(ShapeKind.Queue,   220, 180, 160, 50, "Ingestion Queue", "#FEF3C7");
        var proc = N(ShapeKind.Hexagon, 430, 170, 160, 80, "Stream Processor", "#FFE4E6");
        var wh   = N(ShapeKind.Cylinder, 640, 170, 130, 90, "Warehouse", "#CCFBF1");
        var dash = N(ShapeKind.Rounded, 820, 100, 150, 70, "Dashboards", "#DBEAFE");
        var ml   = N(ShapeKind.Rounded, 820, 240, 150, 70, "ML Models", "#DBEAFE");
        m.Shapes.AddRange(new[] { src1, src2, src3, ing, proc, wh, dash, ml });
        Link(m, src1, ing); Link(m, src2, ing); Link(m, src3, ing);
        Link(m, ing, proc); Link(m, proc, wh);
        Link(m, wh, dash); Link(m, wh, ml);
        Z(m);
        return m;
    }

    private static void Link(DiagramModel m, ShapeNode a, ShapeNode b)
        => m.Connections.Add(new Connection { FromId = a.Id, ToId = b.Id });

    private static void Z(DiagramModel m)
    {
        int i = 1;
        foreach (var s in m.Shapes) s.ZIndex = i++;
    }
}
