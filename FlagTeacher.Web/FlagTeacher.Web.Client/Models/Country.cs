namespace FlagTeacher.Web.Client.Models;

/// <param name="SameFlag">Codes of the other countries with exactly this flag, such as Svalbard for Norway.</param>
public record Country(string Code, string Name, string FlagUrl, IReadOnlyList<string>? SameFlag = null)
{
    /// <summary>Whether <paramref name="code"/> is this country or another one with the same flag.</summary>
    public bool SharesFlagWith(string? code) => code == Code || (code is not null && SameFlag?.Contains(code) == true);
}
