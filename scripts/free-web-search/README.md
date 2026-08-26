# free-web-search runner

`FreeWebSearchProvider`
(`src/Tools/Services/WebSearch/WebSearch.Providers/FreeWebSearchProvider.cs`) shells out to the
`martiendejong/free-web-search` GitHub repo's CLI. That repo is **not** an npm package - it's a
plain Node CLI (`node run.js search "<query>" -n <count> --json`) that must be cloned locally,
once, out of band. This directory is intentionally **not** part of the .NET build.

## One-time setup

```
cd scripts/free-web-search
git clone https://github.com/martiendejong/free-web-search.git vendor/free-web-search
cd vendor/free-web-search
npm install
```

`vendor/` is gitignored - every environment that wants real (non-test) results clones and
installs it locally, the same way the previous `npm ci` step worked for a real npm dependency.

`FreeWebSearchProvider.IsAvailableAsync()` returns `false` (without throwing) if `node` isn't on
`PATH` or `vendor/free-web-search/run.js` can't be found, so a missing/incomplete setup fails
soft rather than crashing callers.

## Search backend

By default the CLI searches DuckDuckGo (HTML scrape, no API key needed) - the same underlying
endpoint `WebSearch.Providers.DuckDuckGoProvider` already uses directly. That means, out of the
box, `FreeWebSearchProvider` is **not** guaranteed to survive a CAPTCHA block that already hit
`DuckDuckGoProvider` from the same IP/host - it's a different code path, not a different network
egress. For real resilience against a blocked default provider, configure the CLI's own optional
backends (see `vendor/free-web-search/README.md` and `SEARXNG-GUIDE.md` after cloning):

- `BRAVE_SEARCH_API_KEY` env var - routes through the Brave Search API instead of DuckDuckGo.
- A self-hosted or public SearXNG instance - see the vendored `SEARXNG-GUIDE.md`.

## Contract

`vendor/free-web-search/run.js` is invoked by `FreeWebSearchProvider` as:

    node run.js search "<query>" -n <count> --json

On success (exit 0) it prints one JSON object to stdout:

    { "provider": "duckduckgo", "query": "...", "results": [{ "title", "url", "snippet", "timestamp" }, ...], "totalResults": N }

Progress/diagnostic lines (e.g. "Trying DuckDuckGo...") go to stderr, not stdout, so they never
pollute the JSON `FreeWebSearchProvider` parses. A non-zero exit code indicates failure; the
stderr message is surfaced by the provider as part of an `HttpRequestException`.
