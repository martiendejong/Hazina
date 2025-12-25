# LLM Instructions for DevGPTTools Project

This document contains important instructions for AI assistants (LLMs) working on this codebase.

## Project Overview

DevGPTTools is a collection of .NET libraries for building AI-powered content generation tools. The solution consists of multiple projects organized into:

- **Common Libraries**: Shared utilities, models, and infrastructure
- **Core Libraries**: Core functionality and data access
- **Services**: Specialized service libraries for various operations
- **Text Extraction**: Tools for extracting text from various file formats

## Critical Requirements

### 1. NuGet Package Publishing

**IMPORTANT**: All projects in this solution are designed to be published as NuGet packages.

When making changes to ANY project:
- Ensure the changes don't break the NuGet packaging process
- Update version numbers appropriately using semantic versioning
- Test the package build before committing

### 2. Publishing Process

The publish script automatically builds and publishes NuGet packages.

**Setup (One-time):**
Set the NuGet API key environment variable:
```powershell
# Windows
setx NUGET_API_KEY "your-api-key"

# Linux/macOS
export NUGET_API_KEY="your-api-key"
```

**Usage:**
```powershell
# Windows
./publish-nuget.ps1

# Linux/macOS
./publish-nuget.sh
```

The script will:
1. Build all projects in Release mode
2. Create packages in `./nupkgs`
3. Automatically publish to NuGet.org (if NUGET_API_KEY is set)
4. Skip duplicates (won't fail if version already exists)

### 3. Project Structure Guidelines

- All projects follow the naming convention: `DevGPT.GenerationTools.*`
- Common libraries are in the `Common/` folder
- Service libraries follow the pattern: `DevGPT.GenerationTools.Services.*`
- All projects target `.NET 8.0` (net8.0-windows)

### 4. Before Making Changes

1. **Understand Dependencies**: Review project references before modifying shared libraries
2. **Test Builds**: Run `dotnet build DevGPTTools.sln` to ensure all projects compile
3. **Check Package Configuration**: Verify .csproj files have proper NuGet metadata if adding new projects

### 5. Adding New Projects

When adding a new project:

1. Follow the naming convention: `DevGPT.GenerationTools.*`
2. Add the project to the solution:
   ```bash
   dotnet sln add path/to/NewProject.csproj
   ```
3. Ensure the .csproj has a `<RootNamespace>` matching the project name
4. Configure NuGet package properties in the .csproj:
   ```xml
   <PropertyGroup>
     <PackageId>DevGPT.GenerationTools.YourProject</PackageId>
     <Version>1.0.0</Version>
     <Authors>Your Name</Authors>
     <Company>Your Company</Company>
     <Description>Description of your package</Description>
     <PackageLicenseExpression>MIT</PackageLicenseExpression>
     <RepositoryUrl>https://github.com/yourusername/DevGPTTools</RepositoryUrl>
   </PropertyGroup>
   ```
5. Test the publish script to ensure your project packages correctly

### 6. Version Management

- Use semantic versioning (MAJOR.MINOR.PATCH)
- Increment PATCH for bug fixes
- Increment MINOR for new features (backwards compatible)
- Increment MAJOR for breaking changes

### 7. Common Tasks

**Build the entire solution:**
```bash
dotnet build DevGPTTools.sln
```

**Restore all dependencies:**
```bash
dotnet restore DevGPTTools.sln
```

**Clean build artifacts:**
```bash
dotnet clean DevGPTTools.sln
```

**Build in Release mode:**
```bash
dotnet build DevGPTTools.sln -c Release
```

## Project Dependencies

Key dependency relationships:
- Most projects depend on `DevGPT.GenerationTools.Common.Models`
- Service projects typically depend on:
  - `DevGPT.GenerationTools.Models`
  - `DevGPT.GenerationTools.Core`
  - `DevGPT.GenerationTools.Data`
- Text extraction functionality is in `DevGPT.GenerationTools.TextExtraction`

## Testing

Before committing changes:
1. Build the solution: `dotnet build DevGPTTools.sln`
2. Run the publish script: `./publish-nuget.ps1` or `./publish-nuget.sh`
3. Verify all packages are created in the `./nupkgs` directory
4. Check for any build warnings or errors

## Questions?

If you're an LLM assistant and unsure about:
- Whether a change will affect NuGet packaging → Always test with the publish script
- Which project to modify → Check the existing project structure and dependencies
- Versioning strategy → Ask the developer for guidance

## Remember

**Every project must successfully build and package**. Always run the publish script after making changes to verify everything still works correctly.
