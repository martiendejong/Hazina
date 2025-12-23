# HTML Mockup Generator

Een web-applicatie die HTML mockups genereert met behulp van AI, met Google OAuth authenticatie en een dagelijkse limiet van 10 pagina's per gebruiker.

## Features

- 🔐 Google OAuth authenticatie
- 🤖 AI-gegenereerde HTML mockups
- 📊 Dagelijkse limiet van 10 pagina's per gebruiker
- 💾 SQLite database voor gebruikersbeheer
- 📱 Responsive design

## Setup

### 1. Google OAuth Configuratie

1. Ga naar de [Google Cloud Console](https://console.cloud.google.com/)
2. Maak een nieuw project aan of selecteer een bestaand project
3. Ga naar "APIs & Services" > "Credentials"
4. Klik op "Create Credentials" > "OAuth 2.0 Client IDs"
5. Selecteer "Web application"
6. Voeg de volgende redirect URIs toe:
   - `https://localhost:5001/signin-google` (voor development)
   - `https://yourdomain.com/signin-google` (voor production)
7. Kopieer de Client ID en Client Secret

### 2. App Configuration

Update `appsettings.Development.json` met je Google OAuth credentials:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    }
  }
}
```

### 3. Dependencies Installeren

```bash
dotnet restore
```

### 4. Database Migratie

De database wordt automatisch aangemaakt bij de eerste run. Als je handmatig migraties wilt uitvoeren:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 5. Applicatie Starten

```bash
dotnet run
```

De applicatie is nu beschikbaar op `https://localhost:5001`

## Gebruik

1. Ga naar de applicatie
2. Klik op "Inloggen met Google"
3. Autoriseer de applicatie met je Google account
4. Begin met het genereren van HTML mockups
5. Je hebt maximaal 10 pagina's per dag

## Database Schema

### Users Table
- `Id` - Unieke gebruikers-ID (Google OAuth sub)
- `Email` - Gebruikers email
- `Name` - Gebruikers naam
- `Picture` - Profielfoto URL (optioneel)
- `CreatedAt` - Aanmaakdatum
- `LastLoginAt` - Laatste login datum
- `LastGenerationDate` - Laatste generatie datum
- `GenerationsToday` - Aantal generaties vandaag

## Technische Details

- **Framework**: ASP.NET Core 8.0
- **Database**: SQLite met Entity Framework Core
- **Authentication**: Google OAuth 2.0
- **AI**: OpenAI GPT (via DevGPT.OpenAI package)
- **UI**: Bootstrap 5

## Beveiliging

- Alle pagina's vereisen authenticatie
- Dagelijkse limiet wordt per gebruiker bijgehouden
- Gebruikersgegevens worden veilig opgeslagen
- HTTPS wordt afgedwongen in productie

## Troubleshooting

### Google OAuth Problemen
- Controleer of de redirect URIs correct zijn geconfigureerd
- Zorg ervoor dat de Google+ API is ingeschakeld
- Controleer of de Client ID en Secret correct zijn

### Database Problemen
- Verwijder `HtmlMockupGenerator.db` en start de applicatie opnieuw
- Controleer of de connection string correct is

### AI Generatie Problemen
- Controleer of de OpenAI API key is geconfigureerd
- Zorg ervoor dat je voldoende API credits hebt 