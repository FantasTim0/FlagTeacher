using FlagTeacher.Web.Components;
using FlagTeacher.Web.Countries;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.Configure<RestCountriesOptions>(builder.Configuration.GetSection(RestCountriesOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<CountryService>((sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IOptions<RestCountriesOptions>>().Value.BaseUrl;
    client.BaseAddress = new Uri(baseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/api/countries", (CountryService countries, CancellationToken ct) => countries.GetCountriesAsync(ct));
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(FlagTeacher.Web.Client._Imports).Assembly);

app.Run();