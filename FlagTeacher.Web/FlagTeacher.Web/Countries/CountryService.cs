using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using FlagTeacher.Web.Client.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FlagTeacher.Web.Countries;

/// <summary>
/// Loads the full country list from restcountries.com once and caches it, so the API key
/// stays on the server and the upstream API is hit at most once per cache lifetime.
/// </summary>
public class CountryService(HttpClient http, IMemoryCache cache, IOptions<RestCountriesOptions> options)
{
    private const int PageSize = 100;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<Country>> GetCountriesAsync(CancellationToken ct)
    {
        var countries = await cache.GetOrCreateAsync("countries", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await FetchAsync(ct);
        });
        return countries!;
    }

    private async Task<IReadOnlyList<Country>> FetchAsync(CancellationToken ct)
    {
        var result = new List<Country>();
        var offset = 0;
        bool more;
        do
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"?response_fields=names.common,codes.alpha_2,flag.url_svg&limit={PageSize}&offset={offset}");
            var apiKey = options.Value.ApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<RestPage>(ct);
            var objects = page?.Data?.Objects ?? [];
            result.AddRange(objects
                .Where(c => !string.IsNullOrWhiteSpace(c.Flag?.UrlSvg) && !string.IsNullOrWhiteSpace(c.Names?.Common))
                .Select(c => new Country(c.Codes?.Alpha2 ?? c.Names!.Common, c.Names!.Common, c.Flag!.UrlSvg!)));

            offset += objects.Count;
            more = objects.Count > 0 && page?.Data?.Meta?.More == true;
        } while (more);

        return result
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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

    private record RestNames([property: JsonPropertyName("common")] string Common);

    private record RestCodes([property: JsonPropertyName("alpha_2")] string? Alpha2);

    private record RestFlag([property: JsonPropertyName("url_svg")] string? UrlSvg);
}
