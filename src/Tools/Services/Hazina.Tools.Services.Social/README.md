# Hazina.Tools.Services.Social

Social media provider framework for importing content from connected accounts.

## Overview

This package provides:
- **Provider Abstraction**: Common interface for social platforms
- **OAuth Management**: Token exchange, refresh, and revocation
- **Content Import**: Posts, articles, comments with engagement metrics
- **SQLite Storage**: Structured storage with full-text search
- **Account Management**: Connect, disconnect, token refresh

## Getting Started
```bash
dotnet build src/Tools/Services/Hazina.Tools.Services.Social/Hazina.Tools.Services.Social.csproj
dotnet add <your>.csproj reference src/Tools/Services/Hazina.Tools.Services.Social/Hazina.Tools.Services.Social.csproj
```

## Providers

### LinkedIn
- OAuth 2.0 authorization code flow
- Profile import (name, email, headline)
- Posts/shares import with engagement
- Token refresh support

### Facebook (existing)
- Page connections
- Posts import with reactions/comments

## Components

### Abstractions
- `ISocialProvider` - Provider interface for OAuth and import
- `ISocialAccountStore` - Connected account persistence
- `ISocialContentStore` - Content storage with search

### Stores
- `SqliteSocialAccountStore` - Account storage in SQLite
- `SqliteSocialContentStore` - Content storage with FTS5 search

### Services
- `SocialImportService` - Orchestrates connections and imports

## Usage

```csharp
// Register providers
services.AddSingleton<ISocialProvider, LinkedInProvider>();
services.AddSingleton<ISocialAccountStore, SqliteSocialAccountStore>();
services.AddSingleton<ISocialContentStore, SqliteSocialContentStore>();
services.AddSingleton<SocialImportService>();

// Connect account
var authUrl = importService.GetAuthorizationUrl("linkedin", redirectUri, state);
// ... OAuth redirect ...
var account = await importService.CompleteAuthorizationAsync(
    projectId, "linkedin", code, redirectUri);

// Import content
var result = await importService.ImportContentAsync(
    projectId, account.Id, new SocialImportOptions { MaxItems = 100 });

// Search content
var results = await importService.SearchContentAsync(
    projectId, "marketing strategy");
```

## SQLite Schema

### connected_accounts
- OAuth tokens (encrypted at rest)
- Import statistics
- Account status

### social_posts
- Content, media URLs
- Engagement (likes, comments, shares)
- FTS5 index for full-text search

### social_articles
- Title, content, summary
- Tags, cover image
- View and engagement counts
