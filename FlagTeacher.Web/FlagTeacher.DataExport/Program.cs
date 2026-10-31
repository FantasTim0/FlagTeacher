using FlagTeacher.DataExport;
using Microsoft.Extensions.Configuration;

// Writes the static data files that the WebAssembly app loads at runtime, so the deployed site needs no
// server and no API key:
//
//   dotnet run --project FlagTeacher.Web/FlagTeacher.DataExport
//
// The exports always run together, the country list first because the outlines are chosen to match it.
// The restcountries.com API key is read from user secrets (RestCountries:ApiKey) or the RestCountries__ApiKey
// environment variable; only the country export needs it.

const string dataDirectory = "FlagTeacher.Web/FlagTeacher.Web.Client/wwwroot/data";

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var data = Path.Combine(FindRepositoryRoot(), dataDirectory);
var countriesFile = Path.Combine(data, "countries.json");

if (!await new CountryExport(configuration).RunAsync(countriesFile))
{
    return 1;
}

if (!await new ShapeExport(configuration).RunAsync(Path.Combine(data, "country-shapes.geojson"), countriesFile))
{
    return 1;
}

return 0;

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !directory.GetFiles("*.slnx").Any())
    {
        directory = directory.Parent;
    }

    return directory?.FullName
           ?? throw new InvalidOperationException("Run from inside the repository.");
}
