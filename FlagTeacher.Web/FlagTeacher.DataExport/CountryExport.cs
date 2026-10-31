using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace FlagTeacher.DataExport;

// Downloads the country list from restcountries.com and writes it as countries.json: the code, the German
// name and the flag URL of every country that has a flag.
internal sealed class CountryExport(IConfiguration configuration)
{
    private const int PageSize = 100;
    private const string GermanLanguageCode = "deu";

    public async Task<bool> RunAsync(string output)
    {
        var baseUrl = configuration["RestCountries:BaseUrl"] ?? "https://api.restcountries.com/countries/v5";
        var apiKey = configuration["RestCountries:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("No API key found. Set it with:");
            Console.Error.WriteLine("  dotnet user-secrets set \"RestCountries:ApiKey\" \"<key>\" --project FlagTeacher.Web/FlagTeacher.DataExport");
            return false;
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
        var german = StringComparer.Create(new CultureInfo("de-DE"), ignoreCase: true);
        var countries = new List<ExportedCountry>();

        var offset = 0;
        bool more;
        do
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"?response_fields=names.common,names.translations,codes.alpha_2,flag.url_svg&limit={PageSize}&offset={offset}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<RestPage>();
            var objects = page?.Data?.Objects ?? [];
            countries.AddRange(objects
                .Where(c => !string.IsNullOrWhiteSpace(c.Flag?.UrlSvg) && !string.IsNullOrWhiteSpace(c.Names?.Common))
                .Select(c =>
                {
                    var name = c.Names!.Translations?.GetValueOrDefault(GermanLanguageCode)?.Common;
                    name = string.IsNullOrWhiteSpace(name) ? c.Names.Common : name;
                    return new ExportedCountry(c.Codes?.Alpha2 ?? c.Names.Common, name, c.Flag!.UrlSvg!);
                }));

            offset += objects.Count;
            more = objects.Count > 0 && page?.Data?.Meta?.More == true;
        } while (more);

        if (countries.Count == 0)
        {
            Console.Error.WriteLine("The API returned no countries; leaving the existing file untouched.");
            return false;
        }

        countries = countries.OrderBy(c => c.Name, german).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(countries, ExportedCountry.JsonOptions));
        Console.WriteLine($"Wrote {countries.Count} countries to {Path.GetFullPath(output)}");
        return true;
    }

    private record RestPage([property: JsonPropertyName("data")] RestData? Data);

    private record RestData(
        [property: JsonPropertyName("objects")] List<RestCountry>? Objects,
        [property: JsonPropertyName("meta")] RestMeta? Meta);

    private record RestMeta([property: JsonPropertyName("more")] bool More);

    private record RestCountry(
        [property: JsonPropertyName("names")] RestNames? Names,
        [property: JsonPropertyName("codes")] RestCodes? Codes,
        [property: JsonPropertyName("flag")] RestFlag? Flag);

    private record RestNames(
        [property: JsonPropertyName("common")] string Common,
        [property: JsonPropertyName("translations")] Dictionary<string, RestTranslation>? Translations);

    private record RestTranslation([property: JsonPropertyName("common")] string? Common);

    private record RestCodes([property: JsonPropertyName("alpha_2")] string? Alpha2);

    private record RestFlag([property: JsonPropertyName("url_svg")] string? UrlSvg);
}

/// <param name="SameFlag">Codes of the other countries with exactly this flag, added by <see cref="FlagExport"/>.</param>
internal record ExportedCountry(string Code, string Name, string FlagUrl, List<string>? SameFlag = null)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
