# Hazina.Auth - Generic Authentication System

**One-command authentication deployment for any ASP.NET Core project**

Hazina.Auth provides a complete authentication system with email/password, JWT tokens, refresh tokens, OAuth (Google, Microsoft), and React frontend components.

## Features

- **ASP.NET Identity** - User management with customizable password policies
- **JWT Authentication** - Secure token-based auth with refresh tokens
- **OAuth 2.0** - Google and Microsoft login support
- **React Components** - Pre-built login, register, and protected route components
- **Entity Framework** - SQLite, PostgreSQL, or SQL Server support
- **One-Command Setup** - Automated deployment script

## Quick Start

### 1. Install Hazina.Auth Packages

```bash
cd your-backend-project
dotnet add package Hazina.Auth.Core
dotnet add package Hazina.Auth.Identity
```

### 2. Run Automated Setup

```powershell
# Basic setup (email/password only)
.\setup-auth.ps1 -ProjectPath "E:\projects\your-project\backend\YourAPI" `
                 -Namespace "YourApp.API" `
                 -Database "SQLite"

# With Google OAuth
.\setup-auth.ps1 -ProjectPath "E:\projects\your-project\backend\YourAPI" `
                 -Namespace "YourApp.API" `
                 -Database "SQLite" `
                 -Providers "Google" `
                 -FrontendPath "E:\projects\your-project\frontend"

# With both Google and Microsoft
.\setup-auth.ps1 -ProjectPath "E:\projects\your-project\backend\YourAPI" `
                 -Namespace "YourApp.API" `
                 -Database "PostgreSQL" `
                 -Providers "Google,Microsoft" `
                 -FrontendPath "E:\projects\your-project\frontend"
```

### 3. Configure Secrets (if using OAuth)

The script generates a random JWT secret automatically. If using OAuth, update `appsettings.json`:

```json
{
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  },
  "Microsoft": {
    "ClientId": "YOUR_MICROSOFT_CLIENT_ID",
    "ClientSecret": "YOUR_MICROSOFT_CLIENT_SECRET"
  }
}
```

### 4. Run Migrations

```bash
cd your-backend-project
dotnet ef migrations add AddHazinaAuth
dotnet ef database update
```

### 5. Test Endpoints

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","fullName":"Test User"}'

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!"}'

# Get current user (requires token)
curl http://localhost:5000/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## What the Script Does

The `setup-auth.ps1` script automates 7 steps:

1. **Add NuGet Packages** - Installs Hazina.Auth.Core and Hazina.Auth.Identity
2. **Create AuthController** - Generates controller from template with your namespace
3. **Update Program.cs** - Adds `AddHazinaAuth()` and `UseHazinaAuth()` calls
4. **Update appsettings.json** - Adds JWT configuration and OAuth settings
5. **Create EF Migration** - Generates and applies database migration
6. **Copy React Components** - Copies auth components to your frontend (if path provided)
7. **Display Summary** - Shows next steps and generated JWT secret

## Manual Setup (Alternative)

If you prefer manual setup instead of the script:

### Backend Setup

**1. Add to Program.cs:**

```csharp
using Hazina.Auth.Identity.Configuration;

// Add before builder.Build()
builder.Services.AddHazinaAuth(builder.Configuration, opts => {
    opts.UseGoogleOAuth = true;  // Optional
    opts.UseMicrosoftOAuth = true;  // Optional
    opts.RequireDigit = true;
    opts.RequiredPasswordLength = 6;
});

// Add after app.UseHttpsRedirection()
app.UseHazinaAuth();
```

**2. Create AuthController:**

Copy `C:\Projects\hazina\templates\backend\AuthController.cs.template` to your Controllers folder and replace `{{NAMESPACE}}` with your namespace.

**3. Update appsettings.json:**

```json
{
  "JWT": {
    "Secret": "YOUR_64_CHARACTER_SECRET_HERE",
    "Issuer": "YourApp",
    "Audience": "YourAppUsers",
    "AccessTokenExpirationMinutes": 60
  }
}
```

**4. Run migrations:**

```bash
dotnet ef migrations add AddHazinaAuth
dotnet ef database update
```

### Frontend Setup

**1. Copy Components:**

Copy all files from `C:\Projects\hazina\templates\frontend\auth\` to your React project.

**2. Wrap App with AuthProvider:**

```tsx
import { AuthProvider } from './components/Auth/useAuth';

function App() {
  return (
    <AuthProvider>
      {/* Your app content */}
    </AuthProvider>
  );
}
```

**3. Use Components:**

```tsx
import { LoginForm } from './components/Auth/LoginForm';
import { RegisterForm } from './components/Auth/RegisterForm';
import { ProtectedRoute } from './components/Auth/ProtectedRoute';
import { GoogleLoginButton } from './components/Auth/GoogleLoginButton';
import { useAuth } from './components/Auth/useAuth';

// Login page
<LoginForm />
<GoogleLoginButton onSuccess={() => navigate('/dashboard')} />

// Register page
<RegisterForm />

// Protected route
<ProtectedRoute>
  <Dashboard />
</ProtectedRoute>

// Use auth hook
const { user, logout, isAuthenticated } = useAuth();
```

## API Endpoints

After setup, your API will have these endpoints:

- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login with email/password
- `POST /api/auth/oauth-login` - Login with OAuth (Google, Microsoft)
- `POST /api/auth/refresh-token` - Refresh access token
- `GET /api/auth/me` - Get current user info (requires authentication)
- `POST /api/auth/logout` - Logout and revoke refresh token

## Configuration Options

### HazinaAuthOptions

```csharp
builder.Services.AddHazinaAuth(builder.Configuration, opts => {
    // OAuth Providers
    opts.UseGoogleOAuth = true;
    opts.UseMicrosoftOAuth = true;

    // Password Requirements
    opts.RequireDigit = true;
    opts.RequireLowercase = true;
    opts.RequireUppercase = true;
    opts.RequireNonAlphanumeric = false;
    opts.RequiredPasswordLength = 6;

    // Custom Database (optional)
    opts.DbContextOptions = dbOpts => {
        dbOpts.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
    };
});
```

### Database Options

**SQLite (Default):**
```csharp
// No configuration needed - uses "Data Source=app.db" by default
```

**PostgreSQL:**
```csharp
opts.DbContextOptions = dbOpts => {
    dbOpts.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
};
```

**SQL Server:**
```csharp
opts.DbContextOptions = dbOpts => {
    dbOpts.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
};
```

## OAuth Setup

### Google OAuth

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials (Web application)
5. Add authorized redirect URI: `https://localhost:5001/signin-google`
6. Copy Client ID and Client Secret to appsettings.json

### Microsoft OAuth

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to Azure Active Directory → App registrations
3. Create new registration
4. Add redirect URI: `https://localhost:5001/signin-microsoft`
5. Create client secret under Certificates & secrets
6. Copy Application (client) ID and client secret to appsettings.json

## Security Best Practices

1. **JWT Secret** - Use a strong 64-character random string (auto-generated by script)
2. **HTTPS Only** - Always use HTTPS in production
3. **Refresh Tokens** - Store refresh tokens securely, rotate on use
4. **Password Policies** - Enforce strong password requirements
5. **OAuth Secrets** - Never commit OAuth secrets to git, use environment variables or Azure Key Vault

## Troubleshooting

### Migration Fails

```bash
# Remove last migration
dotnet ef migrations remove

# Ensure DbContext is registered correctly
# Check that AddHazinaAuth() is called before builder.Build()

# Re-create migration
dotnet ef migrations add AddHazinaAuth
dotnet ef database update
```

### OAuth Login Fails

- Check that OAuth credentials are correct in appsettings.json
- Verify redirect URIs match exactly (case-sensitive)
- Ensure OAuth provider is enabled in Google Cloud Console / Azure Portal
- Check that `UseGoogleOAuth` or `UseMicrosoftOAuth` is set to `true`

### JWT Token Invalid

- Verify JWT secret matches between token generation and validation
- Check token expiration (default 60 minutes)
- Ensure `UseHazinaAuth()` is called after `UseHttpsRedirection()`

## Example Projects

### SEO God

See `E:\projects\seo-god\` for a complete implementation example.

```powershell
# Setup command used:
.\setup-auth.ps1 -ProjectPath "E:\projects\seo-god\backend\SEOGod.API" `
                 -Namespace "SEOGod.API" `
                 -Database "SQLite" `
                 -Providers "Google" `
                 -FrontendPath "E:\projects\seo-god\frontend"
```

## Support

For issues or questions:
- Check existing implementations in client-manager and SEO God projects
- Review generated AuthController for endpoint patterns
- Verify appsettings.json configuration
- Check EF Core migrations applied correctly

## License

MIT License - Use freely in any project
