# API Documentation

This folder contains auto-generated API documentation.

## Local Generation

To generate documentation locally:

```bash
dotnet tool restore
dotnet docfx metadata docfx.json
dotnet docfx build docfx.json
```

Then open `docs/apidoc/index.html` in your browser.

## Automatic Generation

Documentation is automatically generated and committed by GitHub Actions on every push to develop/main branches.
