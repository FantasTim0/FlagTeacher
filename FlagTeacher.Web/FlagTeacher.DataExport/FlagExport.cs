using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace FlagTeacher.DataExport;

// Some territories fly the flag of their country (Svalbard and Norway, for example), so the game accepts any
// of them as the answer. This downloads every flag in countries.json and records, for each country, the
// others whose flag file is identical. It rewrites the file in place, so run it after the country export.
internal sealed class FlagExport
{
    private const int Parallelism = 8;

    public async Task<bool> RunAsync(string countriesFile)
    {
        var countries = JsonSerializer.Deserialize<List<ExportedCountry>>(
            await File.ReadAllTextAsync(countriesFile), ExportedCountry.JsonOptions)!;

        Console.WriteLine($"Comparing {countries.Count} flags");
        using var http = new HttpClient();
        var hashes = new ConcurrentDictionary<string, string>();
        await Parallel.ForEachAsync(countries, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, async (country, token) =>
            hashes[country.Code] = Convert.ToHexString(SHA256.HashData(await http.GetByteArrayAsync(country.FlagUrl, token))));

        countries = countries
            .Select(c =>
            {
                var same = countries.Where(o => o.Code != c.Code && hashes[o.Code] == hashes[c.Code]).Select(o => o.Code).ToList();
                return c with { SameFlag = same.Count > 0 ? same : null };
            })
            .ToList();

        await File.WriteAllTextAsync(countriesFile, JsonSerializer.Serialize(countries, ExportedCountry.JsonOptions));
        var shared = countries.Where(c => c.SameFlag is not null).Select(c => $"{c.Code} ({string.Join(", ", c.SameFlag!)})");
        Console.WriteLine($"Countries sharing a flag: {string.Join(", ", shared)}");
        return true;
    }
}
