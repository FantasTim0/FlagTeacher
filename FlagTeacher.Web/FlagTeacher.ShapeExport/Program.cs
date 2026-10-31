using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Polygon = System.Collections.Generic.List<System.Collections.Generic.List<double[]>>;

// Downloads the country outlines from Natural Earth and writes them as the static GeoJSON file that the
// map loads, keyed by the same alpha-2 codes as countries.json. Run it after the country export, because
// it uses that list to decide which areas need an outline of their own.
//
//   dotnet run --project FlagTeacher.Web/FlagTeacher.ShapeExport [output-path] [countries-path]
//
// Most outlines come from the 50m countries layer. Areas that restcountries lists separately but Natural
// Earth counts as part of a country (French Guiana, Svalbard, ...) are cut out using the 50m map units, and
// codes that are still missing (Gibraltar, Bouvet Island, ...) come from the more detailed 10m layers.
//
// The download location can be changed with the NaturalEarth__BaseUrl environment variable, for example
// to pin a release tag instead of master.

const string defaultOutput = "FlagTeacher.Web/FlagTeacher.Web.Client/wwwroot/data/country-shapes.geojson";
const string defaultCountries = "FlagTeacher.Web/FlagTeacher.Web.Client/wwwroot/data/countries.json";
const int decimals = 2;
const double tolerance = 0.03; // degrees, enough detail for the zoom levels the map allows
const int maxDecimals = 4;

// Natural Earth gives Jan Mayen the code of Norway, restcountries counts it with Svalbard.
var codeOverrides = new Dictionary<string, string> { ["Jan Mayen I."] = "SJ" };

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var baseUrl = configuration["NaturalEarth:BaseUrl"]
              ?? "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/";
var output = args.Length > 0 ? args[0] : Path.Combine(FindRepositoryRoot(), defaultOutput);
var countriesFile = args.Length > 1 ? args[1] : Path.Combine(FindRepositoryRoot(), defaultCountries);

var codes = JsonSerializer.Deserialize<List<ExportedCountry>>(await File.ReadAllTextAsync(countriesFile),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!
    .Select(c => c.Code)
    .ToHashSet();

using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
var shapes = new Dictionary<string, List<Polygon>>();

foreach (var area in await LoadAsync("ne_50m_admin_0_countries"))
{
    Add(area);
}

// Cut out the map units that restcountries lists as an entry of their own, such as French Guiana from France.
var countryCodes = shapes.Keys.ToHashSet();
foreach (var unit in (await LoadAsync("ne_50m_admin_0_map_units")).Where(u => codes.Contains(u.Code) && !countryCodes.Contains(u.Code)))
{
    foreach (var polygons in shapes.Values)
    {
        polygons.RemoveAll(p => unit.Polygons.Any(u => SameRing(p[0], u[0])));
    }

    Add(unit);
}

// Areas too small for the 50m layers.
var missing = codes.Where(c => !shapes.ContainsKey(c)).ToHashSet();
foreach (var layer in new[] { "ne_10m_admin_0_map_units", "ne_10m_admin_0_countries" })
{
    if (missing.Count == 0)
    {
        break;
    }

    var found = (await LoadAsync(layer)).Where(a => missing.Contains(a.Code)).ToList();
    found.ForEach(Add);
    missing.ExceptWith(found.Select(a => a.Code));
}

var features = shapes
    .Where(s => s.Value.Count > 0)
    .OrderBy(s => s.Key, StringComparer.Ordinal)
    .Select(s =>
    {
        var polygons = s.Value.Select(Simplify).ToList();
        return new Feature(new FeatureProperties(s.Key), polygons.Count == 1
            ? new Geometry("Polygon", polygons[0])
            : new Geometry("MultiPolygon", polygons));
    })
    .ToList();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new FeatureCollection(features),
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
Console.WriteLine($"Wrote {features.Count} outlines to {Path.GetFullPath(output)}");
if (missing.Count > 0)
{
    Console.WriteLine($"No outline found for {string.Join(", ", missing.Order())}; the quiz skips these.");
}

return 0;

void Add(Area area)
{
    if (!shapes.TryGetValue(area.Code, out var polygons))
    {
        shapes[area.Code] = polygons = [];
    }

    polygons.AddRange(area.Polygons);
}

async Task<List<Area>> LoadAsync(string layer)
{
    Console.WriteLine($"Downloading {layer}");
    await using var stream = await http.GetStreamAsync($"{layer}.geojson");
    var json = await JsonNode.ParseAsync(stream);
    return json!["features"]!.AsArray()
        .Select(f =>
        {
            var properties = f!["properties"]!;
            var code = codeOverrides.GetValueOrDefault((string)properties["NAME"]!) ?? (string)properties["ISO_A2_EH"]!;
            return new Area(code, ReadPolygons(f["geometry"]!));
        })
        .Where(a => a.Code != "-99")
        .ToList();
}

static List<Polygon> ReadPolygons(JsonNode geometry)
{
    var coordinates = geometry["coordinates"]!.AsArray();
    IEnumerable<JsonArray> polygons = (string)geometry["type"]! == "Polygon"
        ? [coordinates]
        : coordinates.Select(p => p!.AsArray());
    return polygons
        .Select(p => p.Select(r => r!.AsArray().Select(c => new[] { (double)c![0]!, (double)c[1]! }).ToList()).ToList())
        .ToList();
}

static bool SameRing(List<double[]> a, List<double[]> b) =>
    a.Count == b.Count && a.Zip(b).All(p => p.First[0] == p.Second[0] && p.First[1] == p.Second[1]);

// A ring that would collapse at the usual precision (a tiny island) keeps more decimals instead, so it
// still has an outline the map can anchor its dot to.
static Polygon Simplify(Polygon polygon)
{
    var outer = SimplifyRing(polygon[0]) ?? polygon[0];
    return [outer, .. polygon.Skip(1).Select(SimplifyRing).OfType<List<double[]>>()];
}

static List<double[]>? SimplifyRing(List<double[]> ring)
{
    var simplified = DouglasPeucker(Round(ring, decimals));
    if (simplified.Count >= 4)
    {
        return simplified;
    }

    for (var digits = decimals; digits <= maxDecimals; digits++)
    {
        var rounded = Round(ring, digits);
        if (rounded.Count >= 4)
        {
            return rounded;
        }
    }

    return null;
}

static List<double[]> Round(List<double[]> ring, int digits)
{
    var result = new List<double[]>();
    foreach (var point in ring)
    {
        double[] rounded = [Math.Round(point[0], digits), Math.Round(point[1], digits)];
        if (result.Count == 0 || result[^1][0] != rounded[0] || result[^1][1] != rounded[1])
        {
            result.Add(rounded);
        }
    }

    return result;
}

static List<double[]> DouglasPeucker(List<double[]> points)
{
    if (points.Count < 3)
    {
        return points;
    }

    var keep = new bool[points.Count];
    keep[0] = keep[^1] = true;
    Mark(0, points.Count - 1);
    return points.Where((_, i) => keep[i]).ToList();

    void Mark(int first, int last)
    {
        var farthest = -1;
        var distance = tolerance;
        for (var i = first + 1; i < last; i++)
        {
            var d = DistanceToSegment(points[i], points[first], points[last]);
            if (d > distance)
            {
                distance = d;
                farthest = i;
            }
        }

        if (farthest < 0)
        {
            return;
        }

        keep[farthest] = true;
        Mark(first, farthest);
        Mark(farthest, last);
    }
}

static double DistanceToSegment(double[] p, double[] a, double[] b)
{
    double dx = b[0] - a[0], dy = b[1] - a[1];
    var length = dx * dx + dy * dy;
    var t = length == 0 ? 0 : Math.Clamp(((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / length, 0, 1);
    return Math.Sqrt(Math.Pow(p[0] - a[0] - t * dx, 2) + Math.Pow(p[1] - a[1] - t * dy, 2));
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !directory.GetFiles("*.slnx").Any())
    {
        directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException("Run from inside the repository, or pass the output and countries paths as arguments.");
}

internal record Area(string Code, List<Polygon> Polygons);

internal record ExportedCountry(string Code);

internal record FeatureCollection(List<Feature> Features)
{
    [JsonPropertyOrder(-1)] public string Type => "FeatureCollection";
}

internal record Feature(FeatureProperties Properties, Geometry Geometry)
{
    [JsonPropertyOrder(-1)] public string Type => "Feature";
}

internal record FeatureProperties(string Code);

internal record Geometry(string Type, object Coordinates);
