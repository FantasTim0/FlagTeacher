using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

// Downloads the country list from restcountries.com and writes it as a static JSON file that the
// WebAssembly app loads at runtime, so the deployed site needs no server and no API key.
//
//   dotnet run --project FlagTeacher.Web/FlagTeacher.CountryExport [output-path]
//
// The API key is read from user secrets (RestCountries:ApiKey) or the RestCountries__ApiKey
// environment variable.

const string defaultOutput = "FlagTeacher.Web/FlagTeacher.Web.Client/wwwroot/data/countries.json";
const int pageSize = 100;
const string germanLanguageCode = "deu";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Marker>()
    .AddEnvironmentVariables()
    .Build();

var baseUrl = configuration["RestCountries:BaseUrl"] ?? "https://api.restcountries.com/countries/v5";
var apiKey = configuration["RestCountries:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("No API key found. Set it with:");
    Console.Error.WriteLine("  dotnet user-secrets set \"RestCountries:ApiKey\" \"<key>\" --project FlagTeacher.Web/FlagTeacher.CountryExport");
    return 1;
}

var output = args.Length > 0 ? args[0] : Path.Combine(FindRepositoryRoot(), defaultOutput);

using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
var german = StringComparer.Create(new CultureInfo("de-DE"), ignoreCase: true);
var countries = new List<ExportedCountry>();

var offset = 0;
bool more;
do
{
    using var request = new HttpRequestMessage(HttpMethod.Get,
        $"?response_fields=names.common,names.translations,codes.alpha_2,flag.url_svg&limit={pageSize}&offset={offset}");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    using var response = await http.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var page = await response.Content.ReadFromJsonAsync<RestPage>();
    var objects = page?.Data?.Objects ?? [];
    countries.AddRange(objects
        .Where(c => !string.IsNullOrWhiteSpace(c.Flag?.UrlSvg) && !string.IsNullOrWhiteSpace(c.Names?.Common))
        .Select(c =>
        {
            var name = c.Names!.Translations?.GetValueOrDefault(germanLanguageCode)?.Common;
            name = string.IsNullOrWhiteSpace(name) ? c.Names.Common : name;
            return new ExportedCountry(c.Codes?.Alpha2 ?? c.Names.Common, name, c.Flag!.UrlSvg!);
        }));

    offset += objects.Count;
    more = objects.Count > 0 && page?.Data?.Meta?.More == true;
} while (more);

if (countries.Count == 0)
{
    Console.Error.WriteLine("The API returned no countries; leaving the existing file untouched.");
    return 1;
}

countries = countries.OrderBy(c => c.Name, german).ToList();

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(countries, new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
}));
Console.WriteLine($"Wrote {countries.Count} countries to {Path.GetFullPath(output)}");
return 0;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !directory.GetFiles("*.slnx").Any())
    {
        directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException("Run from inside the repository, or pass the output path as an argument.");
}

internal sealed class Marker;

internal record ExportedCountry(string Code, string Name, string FlagUrl);

internal record RestPage([property: JsonPropertyName("data")] RestData? Data);

internal record RestData(
    [property: JsonPropertyName("objects")] List<RestCountry>? Objects,
    [property: JsonPropertyName("meta")] RestMeta? Meta);

internal record RestMeta([property: JsonPropertyName("more")] bool More);

internal record RestCountry(
    [property: JsonPropertyName("names")] RestNames? Names,
    [property: JsonPropertyName("codes")] RestCodes? Codes,
    [property: JsonPropertyName("flag")] RestFlag? Flag);

internal record RestNames(
    [property: JsonPropertyName("common")] string Common,
    [property: JsonPropertyName("translations")] Dictionary<string, RestTranslation>? Translations);

internal record RestTranslation([property: JsonPropertyName("common")] string? Common);

internal record RestCodes([property: JsonPropertyName("alpha_2")] string? Alpha2);

internal record RestFlag([property: JsonPropertyName("url_svg")] string? UrlSvg);
