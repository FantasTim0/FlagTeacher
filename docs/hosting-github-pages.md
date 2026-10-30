# Hosting Flag Teacher on GitHub Pages

Flag Teacher is a static Blazor WebAssembly site: the browser downloads the app, the country list
(`data/countries.json`) and the country outlines (`data/country-shapes.geojson`) and does everything
locally. There is no server and no API key on the hosted site. GitHub Pages is free for public
repositories.

## How it fits together

| Piece | What it does |
| --- | --- |
| `FlagTeacher.Web/FlagTeacher.Web.Client` | The app itself. `dotnet publish` turns it into plain static files. |
| `FlagTeacher.Web/FlagTeacher.CountryExport` | Downloads the country list from restcountries.com (this is where the API key is used) and writes `wwwroot/data/countries.json`. You run it on your machine; the result is committed. |
| `.github/workflows/deploy.yml` | On every push to `main`: publishes the app, fixes the base path and deploys to Pages. |

## One-time setup

1. **Create a public repository on GitHub**, for example `FlagTeacher` (no README or license, so it starts empty).

2. **Make sure the country list is up to date and committed.** It is already in the repository. To
   regenerate it, see [Updating the country list](#updating-the-country-list).

3. **Push the code.** The repository uses jj:

   ```sh
   jj git remote add origin https://github.com/<your-user>/FlagTeacher.git
   jj bookmark create main -r @          # or another revision you want to publish
   jj git push --bookmark main
   ```

4. **Turn on Pages with GitHub Actions as the source.** In the repository on GitHub:
   *Settings → Pages → Build and deployment → Source: **GitHub Actions***.

5. **Run the deployment.** The push in step 3 may already have triggered it, but it fails or is
   skipped if Pages was not enabled yet. Open the *Actions* tab, choose *Deploy to GitHub Pages* and
   click *Run workflow* (or push another change).

6. **Open the site** at `https://<your-user>.github.io/FlagTeacher/`. The exact URL is also shown in
   the workflow run and under *Settings → Pages*.

From then on every push to `main` redeploys the site, usually within a minute or two.

## Updating the country list

Countries rarely change, so the list is exported by hand and committed, not fetched on every deploy.

```sh
# once: store your restcountries.com key (kept outside the repository)
dotnet user-secrets set "RestCountries:ApiKey" "<key>" --project FlagTeacher.Web/FlagTeacher.CountryExport

# whenever you want a fresh list
dotnet run --project FlagTeacher.Web/FlagTeacher.CountryExport
```

This overwrites `FlagTeacher.Web/FlagTeacher.Web.Client/wwwroot/data/countries.json`. Commit and push it
to redeploy. The key can instead be supplied through the `RestCountries__ApiKey` environment variable.

The file is public on the deployed site. It contains only country names, codes and flag URLs, never the key.

## Running locally

```sh
dotnet run --project FlagTeacher.Web/FlagTeacher.Web.Client
```

This serves the site at `http://localhost:5263`. To try the published build the way GitHub serves it:

```sh
dotnet publish FlagTeacher.Web/FlagTeacher.Web.Client -c Release -o release
# serve release/wwwroot with any static file server
```

## Custom domain

*Settings → Pages → Custom domain*, then add the DNS records GitHub shows. If the site then lives at
the root of the domain, the base path must be `/`. Change the workflow step *Set the base path* to
`base="/"`.

## Troubleshooting

- **Blank page, or 404s for `_framework/...` in the browser console:** the base path is wrong. The
  workflow rewrites `<base href>` to `/<repository-name>/`. If you renamed the repository or serve it
  from a custom domain, adjust that step.
- **"Die Länderliste konnte nicht geladen werden":** `data/countries.json` is missing or invalid in the
  deployed files. Run the export again and commit the file.
- **Workflow fails at the deploy step:** Pages is not set to *GitHub Actions* yet (step 4), or the
  repository is private (Pages for private repositories needs a paid GitHub plan).
- **Changes do not show up:** the browser may have cached the old site. Reload with Ctrl/Cmd+Shift+R.
- **Flags do not load:** flag images come from `flags.restcountries.com`. If that host is unreachable,
  the images fail, but the rest of the site works.
