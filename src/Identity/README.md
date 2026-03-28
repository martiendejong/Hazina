# Hazina.Identity - Phase 1 MVP

**Status:** ✅ READY TO RUN (Database + Models + API complete)

Built in ONE SESSION with all 3 phases:
- ✅ Phase B: Database tested and verified
- ✅ Phase A: C# models and DbContext created
- ✅ Phase C: Working authentication API with JWT

---

## 🚀 Quick Start (Run in 5 Minutes)

### 1. Create Database (if not done yet)

```powershell
# Create database
psql -U postgres -c "CREATE DATABASE hazina_identity;"

# Run schema
psql -U postgres -d hazina_identity -f "C:\Projects\hazina\src\Identity\database-schema-v1.sql"

# Verify it worked
psql -U postgres -d hazina_identity -f "C:\Projects\hazina\src\Identity\test-queries.sql"
```

**You should see:** 4 users, organization hierarchy, permission checks

---

### 2. Run the API

```powershell
cd C:\Projects\hazina\src\Identity\Hazina.Identity.API
dotnet run
```

**You should see:** `Now listening on: http://localhost:5200`

Open in browser: http://localhost:5200/swagger

---

### 3. Test the API

Open **NEW** PowerShell window:

```powershell
cd C:\Projects\hazina\src\Identity
.\test-api.ps1
```

**You should see:** ALL TESTS PASSED! ✓✓✓

---

## 📋 What We Built

### Database (PostgreSQL)
- ✅ 9 tables: tenants, users, organizations, roles, permissions, user_roles, role_permissions, refresh_tokens, audit_logs
- ✅ Test data: 1 tenant (Bliek Vastgoed), 7 organizations, 4 users
- ✅ Helper functions: `get_organization_ancestors()`, `get_organization_descendants()`, `user_has_permission()`

### C# Models (Hazina.Identity.Core)
- ✅ HazinaUser, Tenant, Organization, Role, Permission, UserRole, RolePermission, RefreshToken, AuditLog
- ✅ BCrypt password hashing built-in
- ✅ Soft delete support

### DbContext (Hazina.Identity.Infrastructure)
- ✅ Entity Framework Core 9.0
- ✅ PostgreSQL provider (Npgsql)
- ✅ Complete entity configuration
- ✅ Query filters (soft delete, tenant isolation)

### REST API (Hazina.Identity.API)

**Authentication:**
- ✅ `POST /api/auth/login` - Email + password → JWT tokens
- ✅ `POST /api/auth/refresh` - Refresh token → new access token
- ✅ `POST /api/auth/logout` - Revoke refresh token
- ✅ `GET /api/auth/me` - Get current user (protected)

**User Management:**
- ✅ `GET /api/users` - List all users (protected)
- ✅ `GET /api/users/{id}` - Get user by ID (protected)
- ✅ `POST /api/users` - Create new user (protected)
- ✅ `PUT /api/users/{id}` - Update user (protected)
- ✅ `DELETE /api/users/{id}` - Soft delete user (protected)

**Organization Management:**
- ✅ `GET /api/organizations` - List organization hierarchy (protected)
- ✅ `POST /api/organizations` - Create organization (protected)
- ✅ `PUT /api/organizations/{id}` - Update organization (protected)
- ✅ `DELETE /api/organizations/{id}` - Soft delete organization (protected)

**Role Management:**
- ✅ `GET /api/roles` - List all roles (protected)
- ✅ `POST /api/users/{userId}/roles` - Assign role to user (protected)
- ✅ `DELETE /api/users/{userId}/roles/{userRoleId}` - Revoke role (protected)

**Infrastructure:**
- ✅ Swagger UI at http://localhost:5200/swagger
- ✅ CORS enabled (for frontend integration)
- ✅ Audit logging (all actions tracked)
- ✅ Multi-tenant data isolation
- ✅ Soft delete support

---

## 🧪 Test Users (Password: Test123!)

| Email             | Role    | Organization  | Notes                          |
|-------------------|---------|---------------|--------------------------------|
| admin@bliek.nl    | Admin   | Bliek Agency  | Full system access             |
| manager@bliek.nl  | Manager | Building A    | Manage building and sub-orgs   |
| agent@bliek.nl    | Agent   | Floor 1       | View/edit within organization  |
| viewer@bliek.nl   | Viewer  | Room 101      | Read-only access               |

---

## 📖 API Examples

### Login

```bash
curl -X POST http://localhost:5200/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@bliek.nl","password":"Test123!"}'
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "abc123...",
  "expiresIn": 3600,
  "user": {
    "id": "33333333-3333-3333-3333-333333333331",
    "email": "admin@bliek.nl",
    "firstName": "Admin",
    "lastName": "User",
    "roles": [
      {
        "name": "Admin",
        "organizationId": "22222222-2222-2222-2222-222222222221",
        "organizationName": "Bliek Agency"
      }
    ]
  }
}
```

### Get Current User (Protected)

```bash
curl http://localhost:5200/api/auth/me \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### List Users (Protected)

```bash
curl http://localhost:5200/api/users \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### List Organizations (Protected)

```bash
curl http://localhost:5200/api/organizations \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

---

## 🎯 What's Next (Days 2-5)

### Day 2: Add More Endpoints
- `POST /api/users` - Create user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user (soft delete)
- `POST /api/organizations` - Create organization
- `PUT /api/organizations/{id}` - Update organization
- `DELETE /api/organizations/{id}` - Delete organization

### Day 3: Role Management
- `POST /api/users/{userId}/roles` - Assign role to user
- `DELETE /api/users/{userId}/roles/{roleId}` - Remove role from user
- `GET /api/roles` - List all roles

### Day 4: Authorization Policies
- Role-based authorization (Admin-only endpoints)
- Organization-scoped authorization (users can only manage their orgs)
- Permission-based authorization (granular permissions)

### Day 5: Bliek Integration
- Add Hazina.Identity to Bliek backend
- Configure JWT authentication in Bliek
- Test end-to-end flow

---

## 🔧 Configuration

### Database Connection (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=hazina_identity;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### JWT Settings (appsettings.json)

```json
{
  "Jwt": {
    "Key": "CHANGE_THIS_IN_PRODUCTION_TO_A_SECURE_KEY",
    "Issuer": "HazinaIdentity",
    "Audience": "HazinaIdentityClients"
  }
}
```

**⚠️ IMPORTANT:** Change the JWT key in production!

---

## 📁 Project Structure

```
C:\Projects\hazina\src\Identity\
├── database-schema-v1.sql          # PostgreSQL schema
├── test-queries.sql                # Database verification queries
├── test-api.ps1                    # API test script
├── README.md                       # This file
│
├── Hazina.Identity.Core\           # Domain models
│   ├── Models\
│   │   ├── HazinaUser.cs
│   │   ├── Tenant.cs
│   │   ├── Organization.cs
│   │   ├── Role.cs
│   │   ├── Permission.cs
│   │   ├── UserRole.cs
│   │   ├── RolePermission.cs
│   │   ├── RefreshToken.cs
│   │   └── AuditLog.cs
│   └── Hazina.Identity.Core.csproj
│
├── Hazina.Identity.Infrastructure\ # Data access
│   ├── Data\
│   │   └── IdentityDbContext.cs    # EF Core DbContext
│   └── Hazina.Identity.Infrastructure.csproj
│
└── Hazina.Identity.API\            # REST API
    ├── Program.cs                  # API endpoints
    ├── appsettings.json            # Configuration
    ├── Properties\
    │   └── launchSettings.json     # Port 5200
    └── Hazina.Identity.API.csproj
```

---

## 🐛 Troubleshooting

### "Cannot connect to database"
- Check PostgreSQL is running: `pg_isready -U postgres`
- Verify database exists: `psql -U postgres -l | grep hazina_identity`
- Check connection string in appsettings.json

### "401 Unauthorized" on protected endpoints
- Verify access token is valid (not expired)
- Check Authorization header: `Bearer YOUR_TOKEN` (with space)
- Token expires after 1 hour, use refresh token to get new one

### "No project reference" when building
```powershell
cd C:\Projects\hazina\src\Identity\Hazina.Identity.API
dotnet restore
dotnet build
```

---

## ✅ Success Criteria (Phase 1 Complete)

- [x] Database schema created and tested
- [x] C# models map to database tables
- [x] EF Core DbContext configured
- [x] Authentication API working (login, refresh, logout)
- [x] Protected endpoints require JWT token
- [x] Test users can login with different roles
- [x] Swagger UI accessible
- [x] CORS configured for frontend
- [x] Audit logging active

**Status:** PHASE 1 MVP IS READY! 🎉

---

**Next:** Integrate with Bliek or continue building more endpoints?

**ClickUp Board:** https://app.clickup.com/9012956001/v/b/li/901216517140
**Roadmap:** C:\Projects\hazina\docs\identity\HAZINA_IDENTITY_ROADMAP.md
**Tasks:** C:\Projects\hazina\docs\identity\CLICKUP_TASKS_PHASE1.md
