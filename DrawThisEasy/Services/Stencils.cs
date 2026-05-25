using System.Collections.Generic;
using System.Linq;

namespace DrawThisEasy.Services;

/// A cloud-provider service object.
/// Category drives the (generic) glyph drawn on the tile; Color is the provider's brand color.
public record StencilDef(string Id, string Provider, string Name, string Category, string Color);

/// Catalog of cloud-service stencils. These are generic, original glyphs tinted with each
/// provider's brand color plus the factual service name — not the providers' trademarked icons.
public static class Stencils
{
    // Provider brand colors (orange / blue / green keep the three visually distinct).
    public const string AwsColor   = "#FF9900";
    public const string AzureColor = "#0078D4";
    public const string GcpColor   = "#34A853";

    public const string Aws   = "AWS";
    public const string Azure = "Azure";
    public const string Gcp   = "Google Cloud";

    public static readonly IReadOnlyList<StencilDef> All = new List<StencilDef>
    {
        // ---- AWS ----
        new("aws-ec2",        Aws, "EC2",          "compute",    AwsColor),
        new("aws-lambda",     Aws, "Lambda",       "function",   AwsColor),
        new("aws-s3",         Aws, "S3",           "storage",    AwsColor),
        new("aws-rds",        Aws, "RDS",          "database",   AwsColor),
        new("aws-dynamodb",   Aws, "DynamoDB",     "database",   AwsColor),
        new("aws-ecs",        Aws, "ECS / Fargate","container",  AwsColor),
        new("aws-sqs",        Aws, "SQS",          "messaging",  AwsColor),
        new("aws-cloudfront", Aws, "CloudFront",   "network",    AwsColor),
        new("aws-apigw",      Aws, "API Gateway",  "network",    AwsColor),
        new("aws-cloudwatch", Aws, "CloudWatch",   "monitoring", AwsColor),

        // ---- Azure ----
        new("az-vm",          Azure, "Virtual Machines", "compute",    AzureColor),
        new("az-functions",   Azure, "Functions",        "function",   AzureColor),
        new("az-blob",        Azure, "Blob Storage",     "storage",    AzureColor),
        new("az-sql",         Azure, "SQL Database",     "database",   AzureColor),
        new("az-cosmos",      Azure, "Cosmos DB",        "database",   AzureColor),
        new("az-aks",         Azure, "AKS",              "container",  AzureColor),
        new("az-servicebus",  Azure, "Service Bus",      "messaging",  AzureColor),
        new("az-frontdoor",   Azure, "Front Door / CDN", "network",    AzureColor),
        new("az-apim",        Azure, "API Management",   "network",    AzureColor),
        new("az-monitor",     Azure, "Monitor",          "monitoring", AzureColor),

        // ---- Google Cloud ----
        new("gcp-gce",        Gcp, "Compute Engine",   "compute",    GcpColor),
        new("gcp-functions",  Gcp, "Cloud Functions",  "function",   GcpColor),
        new("gcp-gcs",        Gcp, "Cloud Storage",    "storage",    GcpColor),
        new("gcp-sql",        Gcp, "Cloud SQL",        "database",   GcpColor),
        new("gcp-firestore",  Gcp, "Firestore",        "database",   GcpColor),
        new("gcp-gke",        Gcp, "GKE",              "container",  GcpColor),
        new("gcp-pubsub",     Gcp, "Pub/Sub",          "messaging",  GcpColor),
        new("gcp-cdn",        Gcp, "Cloud CDN",        "network",    GcpColor),
        new("gcp-bigquery",   Gcp, "BigQuery",         "analytics",  GcpColor),
        new("gcp-monitoring", Gcp, "Cloud Monitoring", "monitoring", GcpColor),
    };

    private static readonly Dictionary<string, StencilDef> ById =
        All.ToDictionary(s => s.Id);

    public static StencilDef? Find(string? id) =>
        id != null && ById.TryGetValue(id, out var d) ? d : null;

    /// Providers in catalog order (AWS, Azure, Google Cloud).
    public static IEnumerable<string> Providers =>
        All.Select(s => s.Provider).Distinct();

    public static IEnumerable<StencilDef> ForProvider(string provider) =>
        All.Where(s => s.Provider == provider);
}
