# free-web-search runner

Standalone Node.js script bundle consumed by `FreeWebSearchProvider`
(`src/Tools/Services/WebSearch/WebSearch.Providers/FreeWebSearchProvider.cs`).
This directory is intentionally **not** part of the .NET build - it's a plain
Node.js dependency bundle that must be installed once, out of band, before
`FreeWebSearchProvider` can be used for a real (non-test) search.

## One-time setup

```
cd scripts/free-web-search
npm ci
```

`FreeWebSearchProvider.IsAvailableAsync()` returns `false` (without throwing)
if `node` isn't on `PATH` or this directory's `run.cjs` can't be found, so a
missing/incomplete `npm ci` fails soft rather than crashing callers.

## Known issue: verify the package name/version before first install

As of 2026-07-16, `free-web-search` could not be resolved on the public npm
registry (`npm view free-web-search version` and a direct registry lookup
both return 404). `package.json` pins `free-web-search` to `^1.0.0` per the
originating task's fallback instruction, but this is unverified. Before
running `npm ci`/`npm install` here for the first time:

1. Confirm the exact published package name for the intended Puppeteer-based
   Google-scraping library (it may differ from `free-web-search`, or it may
   be scoped/renamed/unpublished).
2. Update the `dependencies` entry in `package.json` accordingly.
3. Re-run `npm install` to regenerate a lock file, then commit it.

Until that's done, `npm ci` in this directory will fail with a 404, and
`FreeWebSearchProvider.SearchAsync` will throw `HttpRequestException` with
the `MODULE_NOT_FOUND` error captured from `run.cjs`'s stderr.

## Contract

`run.cjs` is invoked by `FreeWebSearchProvider` as:

    node run.cjs "<query>" <limit> <lang>

It calls `require('free-web-search')(query, { limit, lang })` and prints a
JSON array of `{ title, url, snippet }` objects to stdout. A non-zero exit
code indicates failure; the stderr message is surfaced by the provider as
part of an `HttpRequestException`.
