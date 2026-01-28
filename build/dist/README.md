# Hazina CLI Release Process

This directory contains scripts and configuration for building and releasing Hazina CLI.

## Release Artifacts

The release process creates the following artifacts:

| Artifact | Platform | Description |
|----------|----------|-------------|
| `hazinacoder-{version}-win-x64-setup.exe` | Windows x64 | Installer with PATH configuration |
| `hazinacoder-{version}-win-x64.zip` | Windows x64 | Portable ZIP archive |
| `hazinacoder-{version}-linux-x64.zip` | Linux x64 | Portable ZIP archive |
| `install.sh` | Linux | One-line installer script |

## Building Locally

### Prerequisites

- .NET 9.0 SDK
- PowerShell 7+ (for build scripts)
- Inno Setup 6 (optional, for Windows installer)

### Build Commands

```powershell
# Build for all platforms
./build-release.ps1

# Build for specific platform
./build-release.ps1 -Platform win
./build-release.ps1 -Platform linux

# Build specific version
./build-release.ps1 -Version "1.2.0"

# Build with Windows installer
./build-release.ps1 -CreateInstaller
```

### Output

Build artifacts are created in `./release-artifacts/`:

```
release-artifacts/
├── win-x64/
│   ├── hazinacoder.exe
│   └── appsettings.json
├── linux-x64/
│   ├── hazinacoder
│   └── appsettings.json
└── installers/
    ├── hazinacoder-{version}-win-x64.zip
    ├── hazinacoder-{version}-linux-x64.zip
    ├── hazinacoder-{version}-win-x64-setup.exe (if -CreateInstaller)
    └── install-hazinacoder.sh
```

## Automated Releases (GitHub Actions)

The release workflow is triggered automatically when you push a version tag:

```bash
# Bump version in project files
./publish-nuget.ps1 patch -NoPublish

# Commit and tag
git add .
git commit -m "Bump version to 1.0.1"
git tag v1.0.1
git push && git push --tags
```

This will:
1. Build self-contained executables for Windows and Linux
2. Create ZIP archives
3. Build Windows installer using Inno Setup
4. Create Linux install script
5. Create GitHub release with all artifacts

### Manual Release

You can also trigger a release manually from GitHub Actions:

1. Go to Actions → Release Hazina CLI
2. Click "Run workflow"
3. Enter the version number (e.g., "1.0.1")
4. Click "Run workflow"

## Installation Methods

### Windows

**Installer (Recommended):**
```
Download and run hazinacoder-{version}-win-x64-setup.exe
```

**Portable:**
```powershell
# Download and extract ZIP
Expand-Archive hazinacoder-{version}-win-x64.zip -DestinationPath C:\hazina
# Add to PATH manually or run directly
C:\hazina\hazinacoder.exe
```

### Linux

**One-line install:**
```bash
curl -fsSL https://github.com/martiendejong/hazina/releases/latest/download/install.sh | bash
```

**Manual:**
```bash
# Download
wget https://github.com/martiendejong/hazina/releases/latest/download/hazinacoder-{version}-linux-x64.zip

# Extract
unzip hazinacoder-{version}-linux-x64.zip -d ~/.hazina

# Make executable
chmod +x ~/.hazina/hazinacoder

# Add to PATH
echo 'export PATH="$HOME/.hazina:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

### .NET Tool (Cross-platform)

```bash
dotnet tool install -g Hazina.App.HazinaCoder
```

## Download Page

The download page is hosted at: **https://martiendejong.nl/hazina/**

## Files in This Directory

| File | Description |
|------|-------------|
| `build-release.ps1` | Main build script |
| `install.sh` | Linux installer script (included in releases) |
| `hazinacoder-setup.iss` | Inno Setup script (auto-generated) |
| `README.md` | This file |

## Versioning

We use semantic versioning (SemVer):
- **MAJOR** version for incompatible API changes
- **MINOR** version for new functionality (backwards compatible)
- **PATCH** version for bug fixes (backwards compatible)

To bump version:
```powershell
# In repository root
./build/publish-nuget.ps1 patch   # 1.0.0 → 1.0.1
./build/publish-nuget.ps1 minor   # 1.0.0 → 1.1.0
./build/publish-nuget.ps1 major   # 1.0.0 → 2.0.0
```
