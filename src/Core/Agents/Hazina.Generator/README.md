# Hazina.Generator

## Purpose
Core generator engine for producing documents/content.

## Getting Started
- Restore/build: `dotnet build src/Core/Agents/Hazina.Generator/Hazina.Generator.csproj`
- Add as project reference if consumed from another project: `dotnet add <your>.csproj reference src/Core/Agents/Hazina.Generator/Hazina.Generator.csproj`

## Usage
- Add reference: `dotnet add <your>.csproj reference src/Core/Agents/Hazina.Generator/Hazina.Generator.csproj`
- Build: `dotnet build src/Core/Agents/Hazina.Generator/Hazina.Generator.csproj`

## API Reference
- XML docs generated on build: `bin/Debug/net8.0/Hazina.Generator.xml`.
- Use IDE tooling or `dotnet doc`/Sandcastle/DocFX to render API docs if desired.

## Examples
```csharp
using Hazina.Generator;
// Instantiate and use the types from Hazina.Generator as needed.
```
