# Google Drive Integration Guide

Store and retrieve Hazina documents directly from Google Drive with full support for folders, permissions, and versioning.

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Google Cloud Setup](#google-cloud-setup)
4. [Installation](#installation)
5. [Authentication](#authentication)
6. [Quick Start](#quick-start)
7. [Document Operations](#document-operations)
8. [Folder Management](#folder-management)
9. [Permissions & Sharing](#permissions--sharing)
10. [Advanced Features](#advanced-features)
11. [Best Practices](#best-practices)
12. [Troubleshooting](#troubleshooting)

---

## Overview

The Google Drive Integration module allows you to:

- ✅ **Store documents** in Google Drive with metadata
- ✅ **Retrieve documents** by ID or query
- ✅ **Organize with folders** - hierarchical structure
- ✅ **Version control** - track document revisions
- ✅ **Share & collaborate** - manage permissions
- ✅ **Search** - powerful query capabilities
- ✅ **Sync** - bidirectional synchronization

**Use Cases:**
- Cloud-based document storage for RAG systems
- Collaborative AI knowledge bases
- Multi-user document management
- Enterprise document workflows
- Backup and archival

---

## Prerequisites

### Required Accounts

1. **Google Account** - Personal or Google Workspace
2. **Google Cloud Project** - Free tier available
3. **Google Drive API** - Enable in Cloud Console

### Required Permissions

- Google Drive API access
- OAuth 2.0 credentials OR Service Account
- Appropriate Drive scopes (read/write)

---

## Google Cloud Setup

### Step 1: Create Google Cloud Project

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Click "Select a project" → "New Project"
3. Enter project name: `Hazina-Drive-Integration`
4. Click "Create"

### Step 2: Enable Google Drive API

1. In Cloud Console, go to "APIs & Services" → "Library"
2. Search for "Google Drive API"
3. Click "Enable"

### Step 3: Create Credentials

#### Option A: OAuth 2.0 (User Authentication)

**Best for:** Interactive applications, user-owned drives

1. Go to "APIs & Services" → "Credentials"
2. Click "Create Credentials" → "OAuth client ID"
3. Configure consent screen:
   - User type: External (for testing) or Internal (for organization)
   - Add scopes: `https://www.googleapis.com/auth/drive.file`
4. Application type: Desktop app or Web application
5. Download JSON file → Save as `credentials.json`

#### Option B: Service Account (Application Authentication)

**Best for:** Server applications, automated workflows

1. Go to "APIs & Services" → "Credentials"
2. Click "Create Credentials" → "Service account"
3. Fill in details:
   - Service account name: `hazina-drive-service`
   - Role: None (we'll use Drive sharing)
4. Click "Create and Continue" → "Done"
5. Click on created service account
6. Go to "Keys" tab → "Add Key" → "Create new key"
7. Choose JSON → Download → Save as `service-account.json`

---

## Installation

```bash
dotnet add package Hazina.Storage.GoogleDrive
```

Or add to .csproj:

```xml
<PackageReference Include="Hazina.Storage.GoogleDrive" Version="2.0.0" />
```

---

## Authentication

### Method 1: OAuth 2.0 User Flow

**Interactive authentication** - Opens browser for user consent.

```csharp
using Hazina.Storage.GoogleDrive;

// Load OAuth credentials
var credentials = await GoogleDriveCredentials.FromOAuthAsync(
    clientSecretsPath: "credentials.json",
    scopes: new[] { "https://www.googleapis.com/auth/drive.file" },
    applicationName: "Hazina RAG System"
);

// First time: Opens browser for user to authorize
// Subsequent times: Uses cached token from ~/.credentials/
```

**credentials.json format:**
```json
{
  "installed": {
    "client_id": "YOUR_CLIENT_ID.apps.googleusercontent.com",
    "client_secret": "YOUR_CLIENT_SECRET",
    "redirect_uris": ["http://localhost"],
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token"
  }
}
```

---

### Method 2: Service Account

**Headless authentication** - No browser interaction needed.

```csharp
var credentials = GoogleDriveCredentials.FromServiceAccount(
    serviceAccountJsonPath: "service-account.json",
    scopes: new[] { "https://www.googleapis.com/auth/drive.file" }
);
```

**service-account.json format:**
```json
{
  "type": "service_account",
  "project_id": "your-project",
  "private_key_id": "...",
  "private_key": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n",
  "client_email": "hazina-drive-service@your-project.iam.gserviceaccount.com",
  "client_id": "...",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token"
}
```

**⚠️ Important for Service Accounts:**
Service accounts have their own Drive - they can't access your personal files unless you explicitly share folders with the service account email.

---

## Quick Start

### Basic Document Upload

```csharp
using Hazina.Storage.GoogleDrive;

// 1. Authenticate
var credentials = await GoogleDriveCredentials.FromOAuthAsync("credentials.json");

// 2. Create Drive store
var driveStore = new GoogleDriveDocumentStore(credentials);

// 3. Upload document
var document = new Document
{
    Id = "doc-001",
    Content = "Hazina is a .NET AI framework...",
    Metadata = new Dictionary<string, object>
    {
        { "category", "documentation" },
        { "version", "2.0" }
    }
};

var fileId = await driveStore.SaveAsync(document);
Console.WriteLine($"Uploaded: https://drive.google.com/file/d/{fileId}");

// 4. Retrieve document
var retrieved = await driveStore.LoadAsync(fileId);
Console.WriteLine($"Content: {retrieved.Content}");
```

---

## Document Operations

### Save Document

```csharp
var document = new Document
{
    Id = "unique-doc-id",
    Content = "Document content here...",
    Metadata = new Dictionary<string, object>
    {
        { "title", "My Document" },
        { "author", "John Doe" },
        { "tags", new[] { "AI", "documentation" } }
    }
};

// Save to root
var fileId = await driveStore.SaveAsync(document);

// Save to specific folder
var fileId = await driveStore.SaveAsync(document, folderId: "folder-123");
```

---

### Load Document

```csharp
// Load by file ID
var doc = await driveStore.LoadAsync("file-id-123");

// Check if exists first
if (await driveStore.ExistsAsync("file-id-123"))
{
    var doc = await driveStore.LoadAsync("file-id-123");
}
```

---

### Update Document

```csharp
// Load existing
var doc = await driveStore.LoadAsync("file-id-123");

// Modify
doc.Content = "Updated content...";
doc.Metadata["version"] = "2.1";
doc.Metadata["updated_at"] = DateTime.UtcNow;

// Save (overwrites existing file, creates new version)
await driveStore.SaveAsync(doc);
```

---

### Delete Document

```csharp
// Move to trash (recoverable)
await driveStore.DeleteAsync("file-id-123");

// Permanent delete (⚠️ irreversible)
await driveStore.DeletePermanentlyAsync("file-id-123");
```

---

### Search Documents

```csharp
// Search by query
var results = await driveStore.SearchAsync(new DriveSearchQuery
{
    NameContains = "documentation",
    MimeType = "text/plain",
    FolderId = "folder-123",  // Optional: search in specific folder
    MaxResults = 100
});

foreach (var file in results)
{
    Console.WriteLine($"{file.Name} - {file.Id}");
}
```

**Advanced Query:**
```csharp
var query = new DriveSearchQuery
{
    Query = "fullText contains 'Hazina' and mimeType='text/plain'",
    OrderBy = "modifiedTime desc",
    MaxResults = 50
};

var results = await driveStore.SearchAsync(query);
```

**Google Drive Query Syntax:**
- `name = 'MyDoc.txt'` - Exact name match
- `name contains 'Doc'` - Partial name match
- `fullText contains 'keyword'` - Content search
- `mimeType = 'text/plain'` - File type filter
- `'folder-id' in parents` - Files in folder
- `modifiedTime > '2024-01-01T00:00:00'` - Date filter

---

## Folder Management

### Create Folder

```csharp
var folderId = await driveStore.CreateFolderAsync(
    folderName: "Hazina Documents",
    parentFolderId: null  // null = root, or specify parent folder
);

Console.WriteLine($"Folder: https://drive.google.com/drive/folders/{folderId}");
```

---

### Organize in Folders

```csharp
// Create folder structure
var projectFolder = await driveStore.CreateFolderAsync("Project Alpha");
var docsFolder = await driveStore.CreateFolderAsync("Documents", projectFolder);
var imagesFolder = await driveStore.CreateFolderAsync("Images", projectFolder);

// Save documents to folders
var doc1 = new Document { Id = "doc1", Content = "..." };
await driveStore.SaveAsync(doc1, folderId: docsFolder);

var doc2 = new Document { Id = "doc2", Content = "..." };
await driveStore.SaveAsync(doc2, folderId: docsFolder);
```

---

### List Folder Contents

```csharp
var files = await driveStore.ListFolderAsync(folderId: "folder-123");

foreach (var file in files)
{
    Console.WriteLine($"{file.Name} ({file.MimeType})");
}
```

---

### Move Files Between Folders

```csharp
await driveStore.MoveFileAsync(
    fileId: "file-123",
    targetFolderId: "new-folder-456"
);
```

---

## Permissions & Sharing

### Share with Specific User

```csharp
await driveStore.ShareWithUserAsync(
    fileId: "file-123",
    emailAddress: "user@example.com",
    role: DrivePermissionRole.Reader  // Reader, Writer, or Owner
);
```

---

### Share with Service Account

When using service accounts, share folders with the service account email:

```csharp
// 1. Get service account email from JSON
// Example: hazina-drive-service@your-project.iam.gserviceaccount.com

// 2. Manually share folder in Drive UI:
//    - Right-click folder → Share
//    - Add service account email
//    - Give "Editor" permission

// 3. Now service account can access that folder
var files = await driveStore.ListFolderAsync("shared-folder-id");
```

---

### Make Public

```csharp
// Anyone with link can view
await driveStore.MakePublicAsync(
    fileId: "file-123",
    role: DrivePermissionRole.Reader
);

// Get shareable link
var link = $"https://drive.google.com/file/d/file-123/view";
```

---

### Remove Permission

```csharp
await driveStore.RevokePermissionAsync(
    fileId: "file-123",
    permissionId: "permission-456"
);
```

---

## Advanced Features

### Version History

Google Drive automatically versions files. Access versions:

```csharp
var versions = await driveStore.GetVersionsAsync("file-123");

foreach (var version in versions)
{
    Console.WriteLine($"Version {version.Id} - {version.ModifiedTime}");
}

// Download specific version
var oldContent = await driveStore.LoadVersionAsync(
    fileId: "file-123",
    versionId: "version-456"
);
```

---

### Metadata Management

```csharp
// Set custom metadata (Drive properties)
await driveStore.SetMetadataAsync(
    fileId: "file-123",
    metadata: new Dictionary<string, string>
    {
        { "embedding_model", "text-embedding-3-small" },
        { "chunk_size", "512" },
        { "indexed_at", DateTime.UtcNow.ToString("o") }
    }
);

// Get metadata
var metadata = await driveStore.GetMetadataAsync("file-123");
```

---

### Batch Operations

```csharp
// Upload multiple documents in parallel
var documents = GetDocumentsToUpload();  // List<Document>

var uploadTasks = documents.Select(doc =>
    driveStore.SaveAsync(doc, folderId: targetFolder)
);

var fileIds = await Task.WhenAll(uploadTasks);

Console.WriteLine($"Uploaded {fileIds.Length} documents");
```

---

### Sync Local ↔ Drive

```csharp
// Sync local folder to Drive
await driveStore.SyncLocalToD riveAsync(
    localPath: @"C:\HazinaData\Documents",
    driveFolderId: "drive-folder-123",
    syncOptions: new SyncOptions
    {
        DeleteRemote = false,  // Don't delete Drive files not in local
        OverwriteNewer = false  // Don't overwrite newer Drive versions
    }
);

// Sync Drive to local
await driveStore.SyncDriveToLocalAsync(
    driveFolderId: "drive-folder-123",
    localPath: @"C:\HazinaData\Documents"
);
```

---

### Export Formats

```csharp
// Export Google Docs to different formats
await driveStore.ExportAsync(
    fileId: "google-doc-id",
    exportFormat: DriveExportFormat.PDF,
    outputPath: "output.pdf"
);

// Available formats: PDF, DOCX, TXT, HTML, EPUB
```

---

## Best Practices

### 1. Use Folders for Organization

```csharp
// Organize by project/category
var projectStructure = new
{
    RootFolder = await driveStore.CreateFolderAsync("Hazina RAG"),
    DocumentsFolder = await driveStore.CreateFolderAsync("Documents", rootId),
    EmbeddingsFolder = await driveStore.CreateFolderAsync("Embeddings", rootId),
    MetadataFolder = await driveStore.CreateFolderAsync("Metadata", rootId)
};
```

---

### 2. Handle Rate Limits

Google Drive API has rate limits (1000 requests per 100 seconds).

```csharp
// Use retry logic
var driveStore = new GoogleDriveDocumentStore(credentials, new DriveStoreOptions
{
    MaxRetries = 3,
    RetryDelayMs = 1000,
    EnableBackoff = true
});

// Batch operations to reduce API calls
var docs = new List<Document> { doc1, doc2, doc3 };
await driveStore.SaveBatchAsync(docs, folderId: targetFolder);
```

---

### 3. Cache Metadata

```csharp
// Cache folder structure
var folderCache = new Dictionary<string, string>();

async Task<string> GetOrCreateFolder(string name, string? parentId = null)
{
    var cacheKey = $"{name}-{parentId}";
    if (folderCache.TryGetValue(cacheKey, out var folderId))
        return folderId;

    folderId = await driveStore.CreateFolderAsync(name, parentId);
    folderCache[cacheKey] = folderId;
    return folderId;
}
```

---

### 4. Error Handling

```csharp
try
{
    await driveStore.SaveAsync(document);
}
catch (GoogleDriveException ex) when (ex.StatusCode == 403)
{
    // Permission denied - check sharing settings
    Console.WriteLine("Permission denied. Is folder shared with service account?");
}
catch (GoogleDriveException ex) when (ex.StatusCode == 404)
{
    // File/folder not found
    Console.WriteLine("File or folder not found");
}
catch (GoogleDriveException ex) when (ex.StatusCode == 429)
{
    // Rate limit exceeded - wait and retry
    await Task.Delay(5000);
    await driveStore.SaveAsync(document);  // Retry
}
```

---

### 5. Secure Credentials

```csharp
// ❌ DON'T commit credentials to git
// credentials.json
// service-account.json

// ✅ DO use environment variables
var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_PATH")
    ?? throw new InvalidOperationException("GOOGLE_CREDENTIALS_PATH not set");

var credentials = await GoogleDriveCredentials.FromOAuthAsync(credentialsPath);

// ✅ DO use secret managers in production
// Azure Key Vault, AWS Secrets Manager, etc.
```

---

## Troubleshooting

### Issue: "Invalid Credentials"

**Cause:** credentials.json or service-account.json malformed

**Fix:**
1. Re-download credentials from Google Cloud Console
2. Verify JSON is valid: `cat credentials.json | jq`
3. Check file permissions: `chmod 600 credentials.json`

---

### Issue: "Access Denied (403)"

**Cause:** Insufficient permissions or folder not shared

**Fix for Service Accounts:**
1. Get service account email from JSON
2. In Google Drive, share folder with that email
3. Give "Editor" permission
4. Wait 1-2 minutes for propagation

**Fix for OAuth:**
1. Delete cached token: `~/.credentials/`
2. Re-run authentication flow
3. Grant all requested permissions

---

### Issue: "File Not Found (404)"

**Cause:** File ID doesn't exist or no access

**Fix:**
```csharp
// Verify file exists
if (!await driveStore.ExistsAsync(fileId))
{
    Console.WriteLine("File doesn't exist or no access");
}

// List accessible files
var files = await driveStore.SearchAsync(new DriveSearchQuery
{
    Query = "'me' in owners",  // Files you own
    MaxResults = 100
});
```

---

### Issue: "Rate Limit Exceeded (429)"

**Cause:** Too many API calls

**Fix:**
```csharp
// Implement exponential backoff
int retries = 0;
while (retries < 5)
{
    try
    {
        await driveStore.SaveAsync(document);
        break;
    }
    catch (GoogleDriveException ex) when (ex.StatusCode == 429)
    {
        var delay = Math.Pow(2, retries) * 1000;  // 1s, 2s, 4s, 8s, 16s
        await Task.Delay((int)delay);
        retries++;
    }
}

// Or use batch operations
await driveStore.SaveBatchAsync(documents);  // Single API call
```

---

### Issue: "Token Expired"

**Cause:** OAuth token expired (default: 1 hour)

**Fix:** Tokens are auto-refreshed, but if issues persist:
```csharp
// Delete cached token to force re-authentication
var tokenPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".credentials", "drive-token.json"
);

if (File.Exists(tokenPath))
    File.Delete(tokenPath);

// Re-authenticate
var credentials = await GoogleDriveCredentials.FromOAuthAsync("credentials.json");
```

---

## Example: RAG System with Drive Storage

Complete example integrating Google Drive with Hazina RAG:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.RAG.Core;
using Hazina.Storage.GoogleDrive;

// 1. Setup AI
var ai = QuickSetup.SetupOpenAI(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!);

// 2. Setup Drive storage
var driveCredentials = await GoogleDriveCredentials.FromOAuthAsync("credentials.json");
var driveStore = new GoogleDriveDocumentStore(driveCredentials);

// 3. Create Drive folder for embeddings
var ragFolderId = await driveStore.CreateFolderAsync("Hazina-RAG-Embeddings");

// 4. Setup RAG with Drive vector store
var vectorStore = new GoogleDriveVectorStore(driveStore, ragFolderId);
var rag = new RAGEngine(ai, vectorStore);

// 5. Index documents from Drive folder
var docsFolderId = "your-docs-folder-id";
var documents = await driveStore.ListFolderAsync(docsFolderId);

foreach (var doc in documents)
{
    var content = await driveStore.LoadAsync(doc.Id);
    await rag.IndexDocumentAsync(content);
}

// 6. Query
var response = await rag.QueryAsync("What is Hazina?");
Console.WriteLine(response.Answer);

// 7. Embeddings are automatically stored in Drive for persistence
```

---

## Further Reading

- [Google Drive API Documentation](https://developers.google.com/drive/api/v3/about-sdk)
- [OAuth 2.0 Guide](https://developers.google.com/identity/protocols/oauth2)
- [Service Accounts Guide](https://cloud.google.com/iam/docs/service-accounts)
- [RAG Guide](RAG_GUIDE.md) - Using Drive storage with RAG
- [API Changelog](API_CHANGELOG.md) - v2.0 changes

---

## Support

- **GitHub Issues:** https://github.com/martiendejong/Hazina/issues
- **Discussions:** https://github.com/martiendejong/Hazina/discussions

---

**Last Updated:** 2026-01-08
**Module Version:** 2.0.0
**Status:** Production Ready ✅
