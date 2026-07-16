#!/usr/bin/env node
'use strict';

// Bundled runner invoked by FreeWebSearchProvider
// (src/Tools/Services/WebSearch/WebSearch.Providers/FreeWebSearchProvider.cs).
//
// Usage: node run.cjs "<query>" <limit> <lang>
//
// On success, prints a JSON array of { title, url, snippet } objects to stdout
// and exits 0. On failure, writes a message to stderr and exits non-zero.

const [, , query, limitArg, langArg] = process.argv;

if (!query || !query.trim()) {
  console.error('run.cjs: query argument is required');
  process.exit(1);
}

const limit = parseInt(limitArg, 10) || 10;
const lang = langArg || 'en';

(async () => {
  try {
    const freeWebSearch = require('free-web-search');
    const results = await freeWebSearch(query, { limit, lang });

    const normalized = (results || []).map((r) => ({
      title: r.title ?? r.Title ?? '',
      url: r.url ?? r.Url ?? r.link ?? '',
      snippet: r.snippet ?? r.Snippet ?? r.description ?? null
    }));

    process.stdout.write(JSON.stringify(normalized));
    process.exit(0);
  } catch (err) {
    console.error(`run.cjs: free-web-search failed: ${err && err.message ? err.message : err}`);
    process.exit(1);
  }
})();
