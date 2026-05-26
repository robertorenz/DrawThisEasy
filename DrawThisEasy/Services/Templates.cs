using System.Collections.Generic;
using DrawThisEasy.Models;

namespace DrawThisEasy.Services;

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
        new("rest-api",      "template.restapi",       "template.restapi.desc",       BuildRestApi()),
        new("serverless",    "template.serverless",    "template.serverless.desc",    BuildServerlessAws()),
        new("three-tier",    "template.threetier",     "template.threetier.desc",     BuildThreeTier()),
        new("kubernetes",    "template.kubernetes",    "template.kubernetes.desc",    BuildKubernetes()),
        new("cicd",          "template.cicd",          "template.cicd.desc",          BuildCicd()),
        new("event-driven",  "template.eventdriven",   "template.eventdriven.desc",   BuildEventDriven()),
        new("frontend-backend","template.frontendbackend","template.frontendbackend.desc", BuildFrontendBackend()),
        new("middleware",    "template.middleware",    "template.middleware.desc",    BuildMessageMiddleware()),
        new("db-cluster",    "template.dbcluster",     "template.dbcluster.desc",     BuildDatabaseCluster()),
        new("full-stack",    "template.fullstack",     "template.fullstack.desc",     BuildFullStack()),
        new("caching",       "template.caching",       "template.caching.desc",       BuildCaching()),
    };

    private static ShapeNode N(ShapeKind k, double x, double y, double w, double h, string label, string? fill = null)
    {
        var n = new ShapeNode { Kind = k, X = x, Y = y, Width = w, Height = h, Label = label };
        if (fill != null) n.Fill = fill;
        return n;
    }

    // A cloud service tile node, sized and colored from the stencil catalog.
    private static ShapeNode Svc(string stencilId, double x, double y)
    {
        var d = Stencils.Find(stencilId)!;
        return new ShapeNode
        {
            Kind = ShapeKind.ServiceTile, Stencil = stencilId,
            X = x, Y = y, Width = 120, Height = 96,
            Label = d.Name, Fill = "#FFFFFF", Stroke = d.Color
        };
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

    private static DiagramModel BuildRestApi()
    {
        var m = new DiagramModel { Title = "REST API" };
        var client = N(ShapeKind.Person,   40, 190, 90, 110, "Client");
        var gw     = N(ShapeKind.Hexagon,  200, 200, 160, 80, "API Gateway", "#FEF3C7");
        var api1   = N(ShapeKind.Server,   430, 70,  110, 130, "API Server 1", "#DBEAFE");
        var api2   = N(ShapeKind.Server,   430, 250, 110, 130, "API Server 2", "#DBEAFE");
        var cache  = N(ShapeKind.Cylinder, 620, 80,  120, 90,  "Redis Cache", "#FFE4E6");
        var db     = N(ShapeKind.Cylinder, 620, 250, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { client, gw, api1, api2, cache, db });
        Link(m, client, gw);
        Link(m, gw, api1); Link(m, gw, api2);
        Link(m, api1, cache); Link(m, api2, cache);
        Link(m, api1, db); Link(m, api2, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildServerlessAws()
    {
        var m = new DiagramModel { Title = "Serverless (AWS)" };
        var user = N(ShapeKind.Person, 30, 190, 90, 110, "User");
        var cf   = Svc("aws-cloudfront", 180, 185);
        var gw   = Svc("aws-apigw",      350, 185);
        var fn   = Svc("aws-lambda",     520, 185);
        var ddb  = Svc("aws-dynamodb",   700, 90);
        var s3   = Svc("aws-s3",         700, 290);
        m.Shapes.AddRange(new[] { user, cf, gw, fn, ddb, s3 });
        Link(m, user, cf); Link(m, cf, gw); Link(m, gw, fn);
        Link(m, fn, ddb); Link(m, fn, s3);
        Z(m);
        return m;
    }

    private static DiagramModel BuildThreeTier()
    {
        var m = new DiagramModel { Title = "Three-tier web app" };
        var browser = N(ShapeKind.Rounded,  60, 195, 150, 70,  "Browser", "#DBEAFE");
        var web     = N(ShapeKind.Server,   280, 165, 110, 130, "Web Server", "#FEF3C7");
        var app     = N(ShapeKind.Server,   470, 165, 110, 130, "App Server", "#DBEAFE");
        var db      = N(ShapeKind.Cylinder, 660, 175, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { browser, web, app, db });
        Link(m, browser, web); Link(m, web, app); Link(m, app, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildKubernetes()
    {
        var m = new DiagramModel { Title = "Kubernetes cluster" };
        var ingress = N(ShapeKind.Hexagon,  60, 200, 150, 70, "Ingress", "#FEF3C7");
        var svc1    = N(ShapeKind.Rounded,  280, 80,  160, 60, "auth deployment", "#DBEAFE");
        var svc2    = N(ShapeKind.Rounded,  280, 195, 160, 60, "orders deployment", "#DBEAFE");
        var svc3    = N(ShapeKind.Rounded,  280, 310, 160, 60, "catalog deployment", "#DBEAFE");
        var db      = N(ShapeKind.Cylinder, 520, 195, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { ingress, svc1, svc2, svc3, db });
        Link(m, ingress, svc1); Link(m, ingress, svc2); Link(m, ingress, svc3);
        Link(m, svc1, db); Link(m, svc2, db); Link(m, svc3, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildCicd()
    {
        var m = new DiagramModel { Title = "CI/CD pipeline" };
        var commit = N(ShapeKind.Rounded,  40, 180, 130, 60,  "Git Commit", "#DBEAFE");
        var build  = N(ShapeKind.Rounded,  210, 180, 120, 60, "Build", "#FEF3C7");
        var test   = N(ShapeKind.Rounded,  360, 180, 120, 60, "Test", "#FEF3C7");
        var deploy = N(ShapeKind.Rounded,  510, 180, 120, 60, "Deploy", "#FFE4E6");
        var prod   = N(ShapeKind.Server,   670, 150, 110, 130, "Production", "#CCFBF1");
        var reg    = N(ShapeKind.Cylinder, 360, 320, 120, 90, "Artifacts", "#CCFBF1");
        m.Shapes.AddRange(new[] { commit, build, test, deploy, prod, reg });
        Link(m, commit, build); Link(m, build, test); Link(m, test, deploy); Link(m, deploy, prod);
        Link(m, build, reg); Link(m, reg, deploy);
        Z(m);
        return m;
    }

    private static DiagramModel BuildEventDriven()
    {
        var m = new DiagramModel { Title = "Event-driven" };
        var producer = N(ShapeKind.Rounded,  40, 190, 150, 60,  "Producer", "#DBEAFE");
        var queue    = N(ShapeKind.Queue,    240, 195, 170, 50, "Message Queue", "#FEF3C7");
        var c1       = N(ShapeKind.Rounded,  470, 100, 150, 60, "Consumer A", "#DBEAFE");
        var c2       = N(ShapeKind.Rounded,  470, 290, 150, 60, "Consumer B", "#DBEAFE");
        var db       = N(ShapeKind.Cylinder, 690, 95,  130, 100, "Database", "#CCFBF1");
        var lake     = N(ShapeKind.Cylinder, 690, 285, 130, 100, "Data Lake", "#CCFBF1");
        m.Shapes.AddRange(new[] { producer, queue, c1, c2, db, lake });
        Link(m, producer, queue); Link(m, queue, c1); Link(m, queue, c2);
        Link(m, c1, db); Link(m, c2, lake);
        Z(m);
        return m;
    }

    private static DiagramModel BuildFrontendBackend()
    {
        var m = new DiagramModel { Title = "Frontend & Backend" };
        var spa   = N(ShapeKind.Rounded,  40, 180, 150, 70,  "SPA Frontend", "#DBEAFE");
        var api   = N(ShapeKind.Server,   250, 150, 110, 130, "Backend API", "#FEF3C7");
        var auth  = N(ShapeKind.Hexagon,  430, 60,  160, 70,  "Auth Middleware", "#FFE4E6");
        var cache = N(ShapeKind.Cylinder, 440, 175, 120, 90,  "Redis Cache", "#FFEDD5");
        var db    = N(ShapeKind.Cylinder, 640, 170, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { spa, api, auth, cache, db });
        Link(m, spa, api); Link(m, api, auth); Link(m, api, cache); Link(m, api, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildMessageMiddleware()
    {
        var m = new DiagramModel { Title = "Message middleware" };
        var p1     = N(ShapeKind.Rounded,  40, 90,  150, 60,  "Orders App", "#DBEAFE");
        var p2     = N(ShapeKind.Rounded,  40, 220, 150, 60,  "Inventory App", "#DBEAFE");
        var broker = N(ShapeKind.Queue,    250, 150, 180, 60, "Message Broker", "#FEF3C7");
        var c1     = N(ShapeKind.Rounded,  500, 70,  150, 60, "Billing Service", "#DBEAFE");
        var c2     = N(ShapeKind.Rounded,  500, 200, 150, 60, "Email Service", "#DBEAFE");
        var db     = N(ShapeKind.Cylinder, 710, 70,  120, 90, "Orders DB", "#CCFBF1");
        var lake   = N(ShapeKind.Cylinder, 710, 200, 120, 90, "Data Lake", "#CCFBF1");
        m.Shapes.AddRange(new[] { p1, p2, broker, c1, c2, db, lake });
        Link(m, p1, broker); Link(m, p2, broker);
        Link(m, broker, c1); Link(m, broker, c2);
        Link(m, c1, db); Link(m, c2, lake);
        Z(m);
        return m;
    }

    private static DiagramModel BuildDatabaseCluster()
    {
        var m = new DiagramModel { Title = "Database cluster" };
        var app     = N(ShapeKind.Server,   40, 150, 110, 130, "App Servers", "#DBEAFE");
        var primary = N(ShapeKind.Cylinder, 240, 165, 130, 100, "Primary DB", "#CCFBF1");
        var r1      = N(ShapeKind.Cylinder, 470, 50,  130, 90,  "Read Replica 1", "#D1FAE5");
        var r2      = N(ShapeKind.Cylinder, 470, 170, 130, 90,  "Read Replica 2", "#D1FAE5");
        var backup  = N(ShapeKind.Cylinder, 470, 290, 130, 90,  "Backup", "#F1F5F9");
        m.Shapes.AddRange(new[] { app, primary, r1, r2, backup });
        Link(m, app, primary); Link(m, primary, r1); Link(m, primary, r2); Link(m, primary, backup);
        Z(m);
        return m;
    }

    private static DiagramModel BuildFullStack()
    {
        var m = new DiagramModel { Title = "Full-stack web" };
        var browser = N(ShapeKind.Rounded,  30, 90,  130, 60,  "Browser", "#DBEAFE");
        var mobile  = N(ShapeKind.Rounded,  30, 220, 130, 60,  "Mobile", "#DBEAFE");
        var cdn     = N(ShapeKind.Cloud,    210, 50,  140, 80,  "CDN", "#E0F2FE");
        var web     = N(ShapeKind.Server,   210, 165, 100, 120, "Web Frontend", "#FEF3C7");
        var gw      = N(ShapeKind.Hexagon,  380, 175, 150, 70,  "API Gateway", "#FFE4E6");
        var backend = N(ShapeKind.Server,   580, 80,  100, 120, "Backend Service", "#DBEAFE");
        var cache   = N(ShapeKind.Cylinder, 580, 230, 120, 80,  "Cache", "#FFEDD5");
        var db      = N(ShapeKind.Cylinder, 740, 150, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { browser, mobile, cdn, web, gw, backend, cache, db });
        Link(m, browser, cdn); Link(m, browser, web); Link(m, mobile, web);
        Link(m, web, gw); Link(m, gw, backend); Link(m, backend, cache); Link(m, backend, db);
        Z(m);
        return m;
    }

    private static DiagramModel BuildCaching()
    {
        var m = new DiagramModel { Title = "Caching layer" };
        var client = N(ShapeKind.Person,   40, 170, 90, 110, "Client");
        var app    = N(ShapeKind.Server,   200, 150, 110, 130, "App Server", "#DBEAFE");
        var cache  = N(ShapeKind.Cylinder, 410, 80,  130, 90,  "Redis Cache", "#FFEDD5");
        var db     = N(ShapeKind.Cylinder, 410, 220, 130, 100, "Database", "#CCFBF1");
        m.Shapes.AddRange(new[] { client, app, cache, db });
        Link(m, client, app); Link(m, app, cache); Link(m, app, db); Link(m, cache, db);
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
