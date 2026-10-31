# Flag Teacher

A small browser game for learning the flags of the world. You're shown a flag and have to name the
country *and* find it on the world map. The interface is in German.

**Play it:** https://fantastim0.github.io/FlagTeacher/

## How it works

- A random flag is shown. Click or tap it to enlarge it.
- Choose the country from the searchable list, and click it on the world map. Tiny countries and islands
  get a dot so they are easy to hit at any zoom level.
- Press **Raten** (guess). A round only counts as correct if both the name and the map location are right.
  After a wrong guess the correct country is highlighted on the map.
- The score for the session (rounds, correct, wrong) is shown at the top. A light and a dark theme are
  available.

All 250 countries and territories from [restcountries.com](https://restcountries.com) are included, each with
its own outline on the map, including overseas regions such as French Guiana, Réunion or Svalbard.

## Tech stack

- [Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/) on .NET 10, running entirely in the
  browser with no backend
- [Leaflet](https://leafletjs.com) for the map, drawn from country outlines only (no map tiles)
- Country names (German) and flags from [restcountries.com](https://restcountries.com)
- Country outlines from [Natural Earth](https://www.naturalearthdata.com) (public domain)
- Hosted on GitHub Pages, deployed by GitHub Actions

## Project structure

| Path | Contents |
| --- | --- |
| `FlagTeacher.Web/FlagTeacher.Web.Client` | The game: Blazor components, map script, styles and the static data in `wwwroot/data` |
| `FlagTeacher.Web/FlagTeacher.DataExport` | Console tool that regenerates the static data from restcountries.com and Natural Earth |
| `.github/workflows/deploy.yml` | Builds and deploys the site on every push to `main` |
| `docs/hosting-github-pages.md` | Hosting, deployment and data update guide |

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet run --project FlagTeacher.Web/FlagTeacher.Web.Client
```

Then open http://localhost:5263.

## Updating the data

The country list (`countries.json`) and the outlines (`country-shapes.geojson`) are committed static files,
so the site needs no server and no API key. To refresh them:

```sh
# once: store your restcountries.com API key
dotnet user-secrets set "RestCountries:ApiKey" "<key>" --project FlagTeacher.Web/FlagTeacher.DataExport

# country list, then the matching outlines
dotnet run --project FlagTeacher.Web/FlagTeacher.DataExport
```

Review the changes, commit and push. See [docs/hosting-github-pages.md](docs/hosting-github-pages.md) for
the details.

## Deployment

Every push to `main` publishes the app and deploys it to GitHub Pages. The one-time setup is described in
[docs/hosting-github-pages.md](docs/hosting-github-pages.md).
