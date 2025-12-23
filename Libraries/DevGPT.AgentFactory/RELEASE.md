# Releaseproces voor DevGPT.AgentFactory

Dit document beschrijft het volledige releaseproces voor de `DevGPT.AgentFactory` NuGet-package, inclusief alle stappen om het pakket te bouwen, (optioneel) te testen en te publiceren op NuGet of een alternatieve feed.

## 1. Vereisten
- .NET 8 SDK geïnstalleerd
- (Optioneel) NuGet CLI geïnstalleerd
- Toegang tot [nuget.org](https://www.nuget.org) (of een alternatieve NuGet feed)
- Geldige NuGet API-key voor publish (zie: https://www.nuget.org/account/apikeys)

## 2. Versienummer controleren

Controleer of het versienummer correct is in het projectbestand `DevGPT.AgentFactory.csproj`. Pas dit zo nodig aan:

```xml
  <PropertyGroup>
    ...
    <Version>1.0.0</Version>
    ...
  </PropertyGroup>
```

Volg [semver](https://semver.org/).

## 3. Project bouwen (Release build)

Open een terminal in de root van het project en voer uit:

```sh
cd DevGPT.AgentFactory
 dotnet build -c Release
```

## 4. NuGet Package genereren

```sh
dotnet pack -c Release
# Output staat standaard in: ./bin/Release/DevGPT.AgentFactory.X.Y.Z.nupkg
```

## 5. Lokaal testen/installeren

Lokaal testen van het .nupkg package vóór publicatie:

### a. Via een lokaal NuGet cache:

- Voeg een lokale feed toe aan je solution (`./local_packages`):

    ```sh
    mkdir -p ../local_packages
    cp ./bin/Release/DevGPT.AgentFactory.*.nupkg ../local_packages/
    ```

- Voeg deze feed toe aan je `NuGet.config`:

    ```xml
    <configuration>
      <packageSources>
        <add key="LocalPackages" value="../local_packages" />
      </packageSources>
    </configuration>
    ```

- Installeer het package in een testproject:
    - Klik met rechts op het project > Manage NuGet Packages > Selecteer je lokale bron > Installeer
    - Of via CLI: `dotnet add package DevGPT.AgentFactory --source ../local_packages`

### b. Testen via Dummy (privé) feed

- Push naar een test-feed zoals [nuget.cloudsmith.io](https://cloudsmith.io/) of een eigen [Azure Artifacts](https://dev.azure.com/) feed:

    ```sh
    dotnet nuget push ./bin/Release/DevGPT.AgentFactory.*.nupkg --source <URL-naar-je-feed> --api-key <API_KEY>
    ```

## 6. Testen van het package

- Maak een (leeg) testproject aan:
    ```sh
    dotnet new console -n TestApp
    cd TestApp
    dotnet add package DevGPT.AgentFactory --source <jouw-lokale-of-dummy-feed>
    dotnet build
    ```
- Importeer een klasse (bv. `DevGPTAgent`) om te testen of alles werkt.

## 7. Publiceren naar NuGet.org

- Publiceer het pakket (pas `<API_KEY>` aan):

    ```sh
    dotnet nuget push ./bin/Release/DevGPT.AgentFactory.*.nupkg --api-key <API_KEY> --source https://api.nuget.org/v3/index.json
    ```

- Na enige tijd staat het pakket live op https://www.nuget.org/packages/DevGPT.AgentFactory

## 8. Extra: CI/CD release pipeline
T.b.v. automatisering (Azure DevOps, GitHub Actions enz.):
- Gebruik stappen zoals hierboven in de juiste YAML-pipeline (build/pack/test/push)
- Bewaar API key veilig als secret of environment variable

## 9. Tips & Troubleshooting
- Geef nooit je NuGet API key weg via repositories
- Check de package-inhoud: `nuget.exe unpack DevGPT.AgentFactory.*.nupkg -Extract` en controleer de DLLs en .nuspec
- Zorg dat de dependencies kloppen

---

**Laatste update:** 2024-06-09
