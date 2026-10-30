namespace FlagTeacher.Web.Countries;

public class RestCountriesOptions
{
    public const string SectionName = "RestCountries";

    public string BaseUrl { get; set; } = "https://api.restcountries.com/countries/v5";

    public string? ApiKey { get; set; }
}
