# Configuration Reference - Hazina Agentic Orchestration

Complete reference for all configuration settings in the Hazina Agentic Orchestration system.

## Table of Contents

1. [Overview](#overview)
2. [Configuration Files](#configuration-files)
3. [Authentication Settings](#authentication-settings)
4. [Orchestration Settings](#orchestration-settings)
5. [Terminal Settings](#terminal-settings)
6. [SignalR Settings](#signalr-settings)
7. [Logging Configuration](#logging-configuration)
8. [OpenAI Integration](#openai-integration)
9. [Environment Variables](#environment-variables)
10. [Production Best Practices](#production-best-practices)

---

## Overview

The Hazina Agentic Orchestration system uses a hierarchical configuration system based on ASP.NET Core configuration:

1. **appsettings.json** - Base configuration (committed to git)
2. **appsettings.Development.json** - Development overrides
3. **appsettings.Production.json** - Production overrides
4. **appsettings.Secrets.json** - Sensitive data (NEVER commit!)
5. **Environment Variables** - Runtime overrides

**Configuration Precedence** (later overrides earlier):
```
appsettings.json →
appsettings.{Environment}.json →
appsettings.Secrets.json →
Environment Variables
```

---

## Configuration Files

### appsettings.json (Base Configuration)

This is the main configuration file with default values and structure.

**Location**: `apps/Demos/Hazina.Demo.AgenticOrchestration/appsettings.json`

**Template**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",

  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://localhost:5123"
      }
    }
  },

  "Authentication": {
    "Enabled": true,
    "Username": "",
    "Password": "",
    "Realm": "Hazina Agentic Orchestration",
    "Jwt": {
      "Enabled": true,
      "SecretKey": "CHANGE_THIS_TO_A_SECURE_RANDOM_KEY_AT_LEAST_32_CHARACTERS_LONG",
      "Issuer": "HazinaOrchestration",
      "Audience": "HazinaOrchestrationClient",
      "AccessTokenExpiryMinutes": 60,
      "RefreshTokenExpiryDays": 7
    }
  },

  "AgenticOrchestration": {
    "DatabasePath": "C:\\scripts\\_machine\\agent-activity.db",
    "LogsPath": "C:\\scripts\\logs",
    "EntitiesYamlPath": "entities.yaml",

    "SignalR": {
      "Enabled": true,
      "HubPath": "/hubs/agentic"
    },

    "Polling": {
      "InstanceHeartbeatTimeoutSeconds": 60,
      "InteractionExpiryMinutes": 60
    },

    "Features": {
      "EnableTaskQueue": true,
      "EnableOutputStreaming": true,
      "EnableRealtimeNotifications": true
    },

    "Terminal": {
      "DefaultCommand": "C:\\scripts\\claude_agent.bat",
      "DefaultWorkingDirectory": "C:\\scripts",
      "DefaultArguments": [],
      "DefaultColumns": 120,
      "DefaultRows": 30,
      "MaxConcurrentSessions": 10,
      "SessionTimeoutMinutes": 60
    },

    "SessionLogging": {
      "Enabled": true,
      "BasePath": "C:\\scripts\\logs\\agent-sessions"
    },

    "Uploads": {
      "Path": "C:\\scripts\\uploads",
      "MaxFileSizeMB": 50
    }
  },

  "Swagger": {
    "Enabled": true,
    "Title": "Hazina Agentic Orchestration API",
    "Description": "Web API for managing Claude Code CLI instances",
    "Version": "v1"
  },

  "OpenAI": {
    "ApiKey": "YOUR_OPENAI_API_KEY_HERE",
    "Model": "gpt-4o-mini",
    "EmbeddingModel": "text-embedding-3-small",
    "ImageModel": "dall-e-3",
    "TtsModel": "gpt-4o-mini-tts"
  }
}
```

### appsettings.Secrets.json (Sensitive Data)

**⚠️ SECURITY WARNING**: This file contains sensitive credentials and MUST NEVER be committed to version control!

**Location**: `apps/Demos/Hazina.Demo.AgenticOrchestration/appsettings.Secrets.json`

**Add to .gitignore**:
```
appsettings.Secrets.json
```

**Template**:
```json
{
  "Authentication": {
    "Username": "your-username-here",
    "Password": "your-secure-password-here",
    "Jwt": {
      "SecretKey": "generate-a-secure-32-character-random-key-here-use-uuid-or-openssl"
    }
  },
  "OpenAI": {
    "ApiKey": "sk-your-actual-openai-api-key-here"
  }
}
```

**Generate secure JWT key**:
```bash
# Using OpenSSL (Linux/macOS/Windows Git Bash)
openssl rand -base64 32

# Using PowerShell (Windows)
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))

# Using Python
python -c "import secrets; print(secrets.token_urlsafe(32))"
```

---

## Authentication Settings

### Basic Authentication

```json
"Authentication": {
  "Enabled": true,           // Enable/disable authentication
  "Username": "admin",        // Basic auth username
  "Password": "secure123",    // Basic auth password
  "Realm": "Hazina Agentic Orchestration"  // Auth realm name
}
```

**When to use**: Quick setup, testing, internal networks

**Security level**: Low (credentials in config file)

### JWT Authentication (Recommended)

```json
"Authentication": {
  "Jwt": {
    "Enabled": true,                   // Enable JWT tokens
    "SecretKey": "your-32-char-key",   // Signing key (MUST be 32+ chars)
    "Issuer": "HazinaOrchestration",   // Token issuer
    "Audience": "HazinaOrchestrationClient",  // Token audience
    "AccessTokenExpiryMinutes": 60,    // Access token lifetime
    "RefreshTokenExpiryDays": 7        // Refresh token lifetime
  }
}
```

**When to use**: Production, multi-user systems, public APIs

**Security level**: High (tokens expire, can be revoked)

**Login flow**:
1. POST `/api/auth/login` with username/password
2. Receive access token and refresh token
3. Use access token in `Authorization: Bearer {token}` header
4. Refresh with `/api/auth/refresh` before expiry

### Disabling Authentication

**For development only**:
```json
"Authentication": {
  "Enabled": false
}
```

**⚠️ WARNING**: Never disable authentication in production!

---

## Orchestration Settings

### Database Configuration

```json
"AgenticOrchestration": {
  "DatabasePath": "C:\\scripts\\_machine\\agent-activity.db"
}
```

**Options**:
- **Windows**: `C:\\scripts\\_machine\\agent-activity.db`
- **Linux/macOS**: `/home/user/scripts/_machine/agent-activity.db`
- **Relative**: `./data/agent-activity.db` (relative to app directory)

**Notes**:
- Database is auto-created on first run
- Uses SQLite (no separate database server required)
- Ensure parent directory exists and is writable
- Backup this file regularly for production

### Logs Configuration

```json
"AgenticOrchestration": {
  "LogsPath": "C:\\scripts\\logs",
  "SessionLogging": {
    "Enabled": true,
    "BasePath": "C:\\scripts\\logs\\agent-sessions"
  }
}
```

**LogsPath**: General application logs
**SessionLogging.BasePath**: Agent session transcripts

**Best practices**:
- Use separate disk/partition for logs (I/O intensive)
- Configure log rotation to prevent disk full
- Monitor disk usage in production

### Entities YAML Path

```json
"AgenticOrchestration": {
  "EntitiesYamlPath": "entities.yaml"
}
```

**Options**:
- **Relative**: `"entities.yaml"` (in app directory)
- **Absolute**: `"C:\\config\\entities.yaml"`

This file defines the data model using Hazina's declarative syntax.

### Polling Configuration

```json
"AgenticOrchestration": {
  "Polling": {
    "InstanceHeartbeatTimeoutSeconds": 60,
    "InteractionExpiryMinutes": 60
  }
}
```

**InstanceHeartbeatTimeoutSeconds**: Agent instance considered inactive after this many seconds without heartbeat

**InteractionExpiryMinutes**: User input requests expire after this many minutes

**Tuning**:
- Short heartbeat timeout (30s): Quick detection of dead agents, more API calls
- Long heartbeat timeout (120s): Fewer false positives, slower detection
- Default 60s is a good balance

### Feature Flags

```json
"AgenticOrchestration": {
  "Features": {
    "EnableTaskQueue": true,
    "EnableOutputStreaming": true,
    "EnableRealtimeNotifications": true
  }
}
```

**EnableTaskQueue**: Submit tasks via API for agents to pick up
**EnableOutputStreaming**: Stream agent output in real-time
**EnableRealtimeNotifications**: SignalR push notifications

**Use cases**:
- Disable features you don't need to reduce overhead
- Enable all for full functionality
- TaskQueue can be disabled if you only use interactive sessions

---

## Terminal Settings

Configuration for launching Claude Code CLI instances:

```json
"AgenticOrchestration": {
  "Terminal": {
    "DefaultCommand": "C:\\scripts\\claude_agent.bat",
    "DefaultWorkingDirectory": "C:\\scripts",
    "DefaultArguments": [],
    "DefaultColumns": 120,
    "DefaultRows": 30,
    "MaxConcurrentSessions": 10,
    "SessionTimeoutMinutes": 60
  }
}
```

### DefaultCommand

Path to the command that launches Claude Code CLI.

**Windows**: `"C:\\scripts\\claude_agent.bat"`
**Linux/macOS**: `"/usr/local/bin/claude"`

### DefaultWorkingDirectory

Starting directory for agent sessions.

**Example**: `"C:\\scripts"` or `"/home/user/projects"`

### DefaultArguments

Command-line arguments passed to Claude CLI.

**Example**:
```json
"DefaultArguments": ["--model", "sonnet", "--verbose"]
```

### Terminal Size

```json
"DefaultColumns": 120,  // Terminal width (characters)
"DefaultRows": 30       // Terminal height (lines)
```

Adjust based on your agent's needs and UI display.

### Session Limits

```json
"MaxConcurrentSessions": 10,     // Max simultaneous agents
"SessionTimeoutMinutes": 60      // Auto-kill inactive sessions
```

**MaxConcurrentSessions**: Prevents resource exhaustion
**SessionTimeoutMinutes**: Cleanup for abandoned sessions

**Tuning**:
- High concurrency needs: Increase MaxConcurrentSessions
- Long-running tasks: Increase SessionTimeoutMinutes
- Resource-constrained systems: Decrease both values

---

## SignalR Settings

Real-time WebSocket communication for live updates:

```json
"AgenticOrchestration": {
  "SignalR": {
    "Enabled": true,
    "HubPath": "/hubs/agentic"
  }
}
```

**Enabled**: Turn SignalR on/off
**HubPath**: WebSocket endpoint URL

**Connection URL**: `wss://localhost:5123/hubs/agentic`

**When to enable**:
- Building a real-time dashboard
- Need instant notifications
- Streaming agent output to UI

**When to disable**:
- API-only usage (no frontend)
- Polling-based architecture
- Resource savings

**CORS for SignalR**:
```json
"Cors": {
  "AllowedOrigins": ["https://yourdomain.com"],
  "AllowCredentials": true
}
```

Add this if your frontend is on a different domain.

---

## Logging Configuration

ASP.NET Core logging system:

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Hazina.AgenticOrchestration": "Debug"
  }
}
```

### Log Levels

- **Trace**: Most verbose, every detail
- **Debug**: Debugging information
- **Information**: General informational messages (default)
- **Warning**: Warnings and non-critical issues
- **Error**: Errors and exceptions
- **Critical**: Critical failures
- **None**: No logging

### Namespace Filtering

```json
"LogLevel": {
  "Default": "Information",                    // All namespaces
  "Microsoft": "Warning",                      // Microsoft.* namespaces
  "Microsoft.AspNetCore.SignalR": "Debug",     // Specific namespace
  "Hazina.AgenticOrchestration": "Debug"       // Your application
}
```

**Production recommendation**:
```json
"LogLevel": {
  "Default": "Information",
  "Microsoft.AspNetCore": "Warning"
}
```

**Development/troubleshooting**:
```json
"LogLevel": {
  "Default": "Debug"
}
```

---

## OpenAI Integration

Configuration for OpenAI API (used by agents):

```json
"OpenAI": {
  "ApiKey": "sk-your-api-key",
  "Model": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-3-small",
  "ImageModel": "dall-e-3",
  "TtsModel": "gpt-4o-mini-tts"
}
```

### Model Selection

**Text Models**:
- `gpt-4o`: Most capable (expensive)
- `gpt-4o-mini`: Balanced performance/cost
- `gpt-3.5-turbo`: Fast and cheap

**Embedding Models**:
- `text-embedding-3-large`: Best quality
- `text-embedding-3-small`: Faster, cheaper
- `text-embedding-ada-002`: Legacy

**Image Models**:
- `dall-e-3`: Latest version
- `dall-e-2`: Older, cheaper

**Usage**:
```csharp
// Injected automatically by Hazina
var llmProvider = serviceProvider.GetRequiredService<ILlmProvider>();
var response = await llmProvider.CompleteAsync("Your prompt here");
```

---

## Environment Variables

Override any setting using environment variables:

### Format

**Hierarchical paths** use double underscores (`__`):

```bash
# JSON path: AgenticOrchestration.DatabasePath
AgenticOrchestration__DatabasePath=C:\custom\path\database.db

# JSON path: Authentication.Jwt.SecretKey
Authentication__Jwt__SecretKey=your-secret-key-here

# JSON path: OpenAI.ApiKey
OpenAI__ApiKey=sk-your-api-key
```

### Common Environment Variables

```bash
# Windows (PowerShell)
$env:AgenticOrchestration__DatabasePath="C:\custom\db.db"
$env:Authentication__Jwt__SecretKey="your-secret-key"
$env:OpenAI__ApiKey="sk-your-key"

# Linux/macOS (Bash)
export AgenticOrchestration__DatabasePath="/opt/data/db.db"
export Authentication__Jwt__SecretKey="your-secret-key"
export OpenAI__ApiKey="sk-your-key"
```

### Docker Environment Variables

```bash
docker run -d \
  -e AgenticOrchestration__DatabasePath=/data/agent-activity.db \
  -e Authentication__Jwt__SecretKey=your-secret-key \
  -e OpenAI__ApiKey=sk-your-key \
  -p 5123:5123 \
  hazina-orchestration
```

### Azure App Service

Set in **Configuration → Application Settings**:
- Name: `AgenticOrchestration__DatabasePath`
- Value: `/home/data/agent-activity.db`

---

## Production Best Practices

### 1. Security

✅ **DO**:
- Use `appsettings.Secrets.json` for sensitive data
- Generate strong random JWT keys (32+ characters)
- Enable HTTPS (TLS 1.2+)
- Rotate API keys regularly
- Use environment variables in cloud deployments
- Enable authentication

❌ **DON'T**:
- Commit secrets to git
- Use default/example keys in production
- Disable authentication
- Expose internal endpoints publicly

### 2. Performance

**Database**:
```json
"AgenticOrchestration": {
  "DatabasePath": "/mnt/fast-ssd/agent-activity.db"
}
```
Use SSD for better SQLite performance.

**Logging**:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information"  // Not Debug in production
  }
}
```

**SignalR**:
```json
"SignalR": {
  "Enabled": true,  // Only if needed
  "MaxConnectionsPerIP": 10  // Prevent abuse
}
```

### 3. Reliability

**Session Timeouts**:
```json
"Terminal": {
  "SessionTimeoutMinutes": 120,  // Longer for production tasks
  "MaxConcurrentSessions": 20    // Scale based on load
}
```

**Database Backups**:
```bash
# Automated backup script (cron/Task Scheduler)
cp /data/agent-activity.db /backups/agent-activity-$(date +%Y%m%d).db
```

**Health Monitoring**:
- Monitor `/health` endpoint
- Set up alerts for failures
- Track database size growth

### 4. Scalability

**Horizontal Scaling**:
- Use shared database (PostgreSQL instead of SQLite)
- Implement Redis for SignalR backplane
- Load balance multiple instances

**Vertical Scaling**:
```json
"Terminal": {
  "MaxConcurrentSessions": 50  // Increase for powerful servers
}
```

### 5. Monitoring

**Application Insights** (Azure):
```json
"ApplicationInsights": {
  "InstrumentationKey": "your-key"
}
```

**Custom Metrics**:
- Active agent count
- Average session duration
- API response times
- Error rates

---

## Configuration Examples

### Example 1: Development (Local Testing)

**appsettings.Development.json**:
```json
{
  "Authentication": {
    "Enabled": false  // No auth for local testing
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "AgenticOrchestration": {
    "DatabasePath": "./dev-database.db",
    "Terminal": {
      "MaxConcurrentSessions": 5
    }
  }
}
```

### Example 2: Production (Cloud Deployment)

**appsettings.Production.json**:
```json
{
  "Authentication": {
    "Enabled": true,
    "Jwt": {
      "AccessTokenExpiryMinutes": 30
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AgenticOrchestration": {
    "DatabasePath": "/mnt/data/agent-activity.db",
    "Terminal": {
      "MaxConcurrentSessions": 50,
      "SessionTimeoutMinutes": 180
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:5123"
      }
    }
  }
}
```

### Example 3: High-Security Environment

```json
{
  "Authentication": {
    "Enabled": true,
    "Jwt": {
      "AccessTokenExpiryMinutes": 15,  // Short-lived tokens
      "RefreshTokenExpiryDays": 1       // Daily refresh required
    }
  },
  "RateLimiting": {
    "Enabled": true,
    "RequestsPerMinute": 60
  },
  "Cors": {
    "AllowedOrigins": ["https://trusted-domain.com"],  // Whitelist only
    "AllowCredentials": true
  }
}
```

---

## Troubleshooting Configuration

### Issue: "Configuration value not found"

**Cause**: Missing setting or typo in key name

**Solution**:
```bash
# Verify configuration is loaded
dotnet run --configuration Development

# Check merged configuration
dotnet user-secrets list  # If using user secrets
```

### Issue: "JWT token validation fails"

**Cause**: Mismatch between token issuer/audience in config and token

**Solution**:
- Ensure `Jwt.Issuer` and `Jwt.Audience` match between server and client
- Regenerate tokens after changing these values

### Issue: "Database locked"

**Cause**: SQLite doesn't handle high concurrency well

**Solution**:
- Reduce `MaxConcurrentSessions`
- OR migrate to PostgreSQL for production:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=hazina;Username=user;Password=pass"
  }
  ```

---

## Support

- **Installation Guide**: [INSTALLATION.md](./INSTALLATION.md)
- **GitHub**: https://github.com/martiendejong/Hazina
- **Issues**: https://github.com/martiendejong/Hazina/issues

---

**Configuration complete!** 🔧

For deployment instructions, see [INSTALLATION.md](./INSTALLATION.md).
