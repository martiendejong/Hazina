# Hazina.Identity Phase 1 - ClickUp Task Breakdown

**ClickUp Board:** https://app.clickup.com/9012956001/v/b/li/901216517140
**Timeline:** 12 weeks (Months 1-3)
**Goal:** MVP for Bliek - RBAC + multi-tenant

---

## Epic 1: Architecture & Setup (Weeks 1-2)

### Task 1.1: Architecture Design Document
**Priority:** Urgent
**Tags:** phase-1, architecture, documentation
**Assignee:** You
**Time Estimate:** 16 hours

**Description:**
Design the complete architecture for Hazina.Identity Phase 1 MVP.

**Acceptance Criteria:**
- [ ] Organization hierarchy model (Agency → Building → Floor → Room)
- [ ] Role-based access model (Admin, Manager, Agent, Viewer)
- [ ] API contracts defined (REST endpoints, request/response schemas)
- [ ] Database schema (ERD diagram with all tables)
- [ ] Authentication flow (login, refresh token, logout)
- [ ] Authorization flow (role checking, permission evaluation)
- [ ] Integration points with Bliek documented
- [ ] Technology stack confirmed (OpenIddict vs Duende decision)
- [ ] Document published: `hazina/docs/identity/architecture.md`

**Deliverable:** Architecture document in Markdown with diagrams

---

### Task 1.2: Repository Structure Creation
**Priority:** Urgent
**Tags:** phase-1, infrastructure, setup
**Assignee:** You
**Time Estimate:** 4 hours

**Description:**
Create the Hazina.Identity directory structure and .csproj files in the Hazina repository.

**Acceptance Criteria:**
- [ ] Folder structure created: `C:\Projects\hazina\src\Identity\`
- [ ] Core projects:
  - [ ] `Hazina.Identity.Core.csproj`
  - [ ] `Hazina.Identity.Infrastructure.csproj`
  - [ ] `Hazina.Identity.OIDC.csproj`
  - [ ] `Hazina.Identity.API.csproj`
- [ ] Test projects:
  - [ ] `Hazina.Identity.Core.Tests.csproj`
  - [ ] `Hazina.Identity.API.Tests.csproj`
  - [ ] `Hazina.Identity.Integration.Tests.csproj`
- [ ] Projects added to `Hazina.sln`
- [ ] All projects target .NET 9.0
- [ ] NuGet package references added (EF Core, OpenIddict, etc.)
- [ ] Projects build successfully (`dotnet build`)

**Deliverable:** Working repository structure, all projects compile

---

### Task 1.3: Database Schema Design
**Priority:** Urgent
**Tags:** phase-1, database, architecture
**Assignee:** You
**Time Estimate:** 8 hours

**Description:**
Design the PostgreSQL database schema for users, organizations, roles, permissions, and audit logs.

**Acceptance Criteria:**
- [ ] ERD diagram created (use dbdiagram.io or draw.io)
- [ ] Tables designed:
  - [ ] `Users` (Id, Email, PasswordHash, CreatedAt, UpdatedAt)
  - [ ] `Organizations` (Id, Name, Type, ParentId, TenantId)
  - [ ] `Roles` (Id, Name, Description, TenantId)
  - [ ] `Permissions` (Id, Resource, Action, TenantId)
  - [ ] `UserRoles` (UserId, RoleId, OrganizationId)
  - [ ] `RolePermissions` (RoleId, PermissionId)
  - [ ] `AuditLogs` (Id, UserId, Action, Resource, Timestamp, Details)
  - [ ] `RefreshTokens` (Id, UserId, Token, ExpiresAt, IsRevoked)
- [ ] Indexes defined for performance
- [ ] Foreign keys and constraints defined
- [ ] Multi-tenant isolation strategy (TenantId in all tables)
- [ ] Soft delete support (IsDeleted flag)
- [ ] Schema documented in architecture.md

**Deliverable:** ERD diagram + SQL schema script

---

### Task 1.4: Technology Evaluation - OpenIddict vs Duende
**Priority:** High
**Tags:** phase-1, architecture, decision
**Assignee:** You
**Time Estimate:** 4 hours

**Description:**
Evaluate OpenIddict vs Duende IdentityServer for OIDC provider. Make final decision.

**Acceptance Criteria:**
- [ ] OpenIddict evaluation:
  - [ ] License: Apache 2.0 (fully open-source)
  - [ ] .NET 9.0 compatibility
  - [ ] Feature set (OIDC, JWT, refresh tokens)
  - [ ] Community support, documentation quality
  - [ ] Active maintenance (GitHub activity)
- [ ] Duende IdentityServer evaluation:
  - [ ] License: Community Edition limitations (users, revenue)
  - [ ] Commercial pricing ($1,500/year for Enterprise)
  - [ ] Feature set comparison
  - [ ] Migration path if we outgrow Community Edition
- [ ] Decision matrix (criteria: cost, features, licensing, support)
- [ ] Final decision documented in architecture.md
- [ ] Rationale explained (why chosen over alternative)

**Deliverable:** Decision document with rationale

**Recommendation:** OpenIddict (open-source, no licensing issues, .NET 9.0 native)

---

### Task 1.5: API Contract Definition
**Priority:** High
**Tags:** phase-1, api, documentation
**Assignee:** You
**Time Estimate:** 6 hours

**Description:**
Define all REST API endpoints, request/response schemas, authentication requirements.

**Acceptance Criteria:**
- [ ] Authentication endpoints:
  - [ ] `POST /api/auth/login` (email, password → access_token, refresh_token)
  - [ ] `POST /api/auth/refresh` (refresh_token → new access_token)
  - [ ] `POST /api/auth/logout` (revoke refresh token)
  - [ ] `GET /api/auth/me` (get current user info)
- [ ] User management endpoints:
  - [ ] `GET /api/users` (list users, paginated, filtered)
  - [ ] `GET /api/users/{id}` (get user details)
  - [ ] `POST /api/users` (create user, send invite email)
  - [ ] `PUT /api/users/{id}` (update user)
  - [ ] `DELETE /api/users/{id}` (soft delete user)
- [ ] Organization endpoints:
  - [ ] `GET /api/organizations` (list orgs, hierarchical)
  - [ ] `GET /api/organizations/{id}` (get org details)
  - [ ] `POST /api/organizations` (create org)
  - [ ] `PUT /api/organizations/{id}` (update org)
  - [ ] `DELETE /api/organizations/{id}` (soft delete org)
- [ ] Role endpoints:
  - [ ] `GET /api/roles` (list roles)
  - [ ] `POST /api/roles` (create role)
  - [ ] `POST /api/users/{userId}/roles` (assign role to user)
  - [ ] `DELETE /api/users/{userId}/roles/{roleId}` (remove role)
- [ ] OpenAPI/Swagger spec generated
- [ ] Request/response examples provided
- [ ] Error responses documented (400, 401, 403, 404, 500)
- [ ] Authentication requirements specified (JWT bearer token)

**Deliverable:** OpenAPI spec (`swagger.json`) + API reference doc

---

## Epic 2: Core Implementation (Weeks 3-6)

### Task 2.1: Hazina.Identity.Core - Models
**Priority:** Urgent
**Tags:** phase-1, core, models
**Assignee:** Senior .NET Engineer
**Time Estimate:** 8 hours

**Description:**
Implement core domain models (User, Organization, Role, Permission, etc.)

**Acceptance Criteria:**
- [ ] `HazinaUser.cs` (Id, Email, PasswordHash, CreatedAt, UpdatedAt, TenantId)
- [ ] `Organization.cs` (Id, Name, Type, ParentId, TenantId, hierarchy methods)
- [ ] `Role.cs` (Id, Name, Description, Permissions, TenantId)
- [ ] `Permission.cs` (Id, Resource, Action, Description)
- [ ] `AuditLog.cs` (Id, UserId, Action, Resource, Timestamp, Details)
- [ ] `RefreshToken.cs` (Id, UserId, Token, ExpiresAt, IsRevoked)
- [ ] `TenantContext.cs` (current tenant resolution)
- [ ] All models have XML documentation comments
- [ ] All models implement `IEntity` interface (Id, TenantId)
- [ ] Unit tests for model validation rules (>80% coverage)

**Deliverable:** Domain models with tests

---

### Task 2.2: Hazina.Identity.Core - Abstractions
**Priority:** Urgent
**Tags:** phase-1, core, interfaces
**Assignee:** Senior .NET Engineer
**Time Estimate:** 6 hours

**Description:**
Define core service interfaces for dependency injection.

**Acceptance Criteria:**
- [ ] `IIdentityService.cs` (user management operations)
  - [ ] `Task<HazinaUser> CreateUserAsync(CreateUserRequest request)`
  - [ ] `Task<HazinaUser> GetUserByIdAsync(string userId)`
  - [ ] `Task<HazinaUser> GetUserByEmailAsync(string email)`
  - [ ] `Task<bool> ValidatePasswordAsync(string userId, string password)`
  - [ ] `Task UpdatePasswordAsync(string userId, string newPassword)`
  - [ ] `Task DeleteUserAsync(string userId)`
- [ ] `IOrganizationService.cs` (organization hierarchy operations)
  - [ ] `Task<Organization> CreateOrganizationAsync(CreateOrgRequest request)`
  - [ ] `Task<Organization> GetOrganizationAsync(string orgId)`
  - [ ] `Task<List<Organization>> GetChildOrganizationsAsync(string parentId)`
  - [ ] `Task<List<Organization>> GetOrganizationHierarchyAsync(string orgId)`
- [ ] `IRoleService.cs` (role management operations)
  - [ ] `Task<Role> CreateRoleAsync(CreateRoleRequest request)`
  - [ ] `Task AssignRoleToUserAsync(string userId, string roleId, string orgId)`
  - [ ] `Task<List<Role>> GetUserRolesAsync(string userId, string orgId)`
- [ ] `IAuthorizationService.cs` (authorization checks)
  - [ ] `Task<bool> UserHasPermissionAsync(string userId, string resource, string action)`
  - [ ] `Task<bool> UserHasRoleAsync(string userId, string roleName, string orgId)`
- [ ] `IAuditService.cs` (audit logging)
  - [ ] `Task LogAsync(string userId, string action, string resource, object details)`
- [ ] All interfaces have XML documentation
- [ ] Async/await pattern used throughout

**Deliverable:** Service interfaces

---

### Task 2.3: Hazina.Identity.Infrastructure - Data Access
**Priority:** Urgent
**Tags:** phase-1, infrastructure, database
**Assignee:** Senior .NET Engineer
**Time Estimate:** 12 hours

**Description:**
Implement Entity Framework Core DbContext, repositories, and data access layer.

**Acceptance Criteria:**
- [ ] `IdentityDbContext.cs` with DbSets:
  - [ ] `DbSet<HazinaUser> Users`
  - [ ] `DbSet<Organization> Organizations`
  - [ ] `DbSet<Role> Roles`
  - [ ] `DbSet<Permission> Permissions`
  - [ ] `DbSet<UserRole> UserRoles`
  - [ ] `DbSet<RolePermission> RolePermissions`
  - [ ] `DbSet<AuditLog> AuditLogs`
  - [ ] `DbSet<RefreshToken> RefreshTokens`
- [ ] Entity configurations (Fluent API):
  - [ ] Indexes (Email, TenantId, ParentId)
  - [ ] Foreign keys and cascade rules
  - [ ] Required fields, max lengths
  - [ ] Unique constraints
- [ ] Multi-tenant query filter (automatic TenantId filtering)
- [ ] Soft delete query filter (automatic IsDeleted = false)
- [ ] Audit interceptor (auto-populate CreatedAt, UpdatedAt)
- [ ] Initial EF Core migration created
- [ ] Migration applies successfully to PostgreSQL
- [ ] Connection string configuration (appsettings.json)
- [ ] Database seeding (default roles: Admin, Manager, Agent, Viewer)

**Deliverable:** DbContext + migrations

---

### Task 2.4: Hazina.Identity.Infrastructure - Services
**Priority:** Urgent
**Tags:** phase-1, infrastructure, business-logic
**Assignee:** Senior .NET Engineer
**Time Estimate:** 16 hours

**Description:**
Implement business logic services (IdentityService, OrganizationService, RoleService, etc.)

**Acceptance Criteria:**
- [ ] `IdentityService.cs` implements `IIdentityService`:
  - [ ] User CRUD operations
  - [ ] Password hashing (BCrypt or PBKDF2)
  - [ ] Password validation
  - [ ] Email uniqueness check
  - [ ] Tenant isolation enforcement
- [ ] `OrganizationService.cs` implements `IOrganizationService`:
  - [ ] Organization CRUD operations
  - [ ] Hierarchy navigation (parents, children, ancestors)
  - [ ] Circular reference prevention
  - [ ] Tenant isolation enforcement
- [ ] `RoleService.cs` implements `IRoleService`:
  - [ ] Role CRUD operations
  - [ ] Role assignment to users
  - [ ] Permission assignment to roles
  - [ ] Duplicate role check
- [ ] `AuthorizationService.cs` implements `IAuthorizationService`:
  - [ ] Permission evaluation (user → roles → permissions)
  - [ ] Organization-scoped authorization
  - [ ] Caching for performance (IMemoryCache)
- [ ] `AuditService.cs` implements `IAuditService`:
  - [ ] Audit log creation
  - [ ] Async logging (fire-and-forget pattern)
  - [ ] Structured JSON details
- [ ] All services use dependency injection
- [ ] All services have XML documentation
- [ ] Unit tests for all services (>80% coverage)
- [ ] Integration tests with in-memory database

**Deliverable:** Service implementations + tests

---

### Task 2.5: Hazina.Identity.OIDC - OpenIddict Configuration
**Priority:** Urgent
**Tags:** phase-1, oidc, authentication
**Assignee:** Senior .NET Engineer
**Time Estimate:** 12 hours

**Description:**
Configure OpenIddict for JWT token issuance, validation, refresh tokens.

**Acceptance Criteria:**
- [ ] OpenIddict NuGet packages added:
  - [ ] OpenIddict.AspNetCore
  - [ ] OpenIddict.EntityFrameworkCore
- [ ] OpenIddict configuration in `Program.cs`:
  - [ ] Server configured (allow password flow, refresh token flow)
  - [ ] Scopes defined (openid, profile, email, roles)
  - [ ] Token lifetimes (access: 1 hour, refresh: 30 days)
  - [ ] Encryption keys (development: ephemeral, production: X.509 cert)
  - [ ] Token format: JWT (not reference tokens)
- [ ] Claims transformation:
  - [ ] Add user ID as "sub" claim
  - [ ] Add email as "email" claim
  - [ ] Add roles as "role" claims
  - [ ] Add tenant ID as "tenant_id" claim
  - [ ] Add organization IDs as "org_ids" claims
- [ ] Refresh token storage (database-backed)
- [ ] Refresh token revocation on logout
- [ ] Token validation middleware configured
- [ ] CORS configuration for frontend
- [ ] HTTPS enforcement in production

**Deliverable:** Working OIDC provider

---

### Task 2.6: Hazina.Identity.API - Controllers
**Priority:** Urgent
**Tags:** phase-1, api, controllers
**Assignee:** Senior .NET Engineer
**Time Estimate:** 16 hours

**Description:**
Implement REST API controllers for users, organizations, roles, authentication.

**Acceptance Criteria:**
- [ ] `AuthController.cs`:
  - [ ] `POST /api/auth/login` (username/password → tokens)
  - [ ] `POST /api/auth/refresh` (refresh token → new access token)
  - [ ] `POST /api/auth/logout` (revoke refresh token)
  - [ ] `GET /api/auth/me` (get current user from JWT)
  - [ ] Input validation (FluentValidation)
  - [ ] Error handling (401 Unauthorized for bad credentials)
- [ ] `UsersController.cs`:
  - [ ] `GET /api/users` (list, paginated, requires Admin role)
  - [ ] `GET /api/users/{id}` (get user, requires Admin or self)
  - [ ] `POST /api/users` (create user, requires Admin, sends invite email)
  - [ ] `PUT /api/users/{id}` (update user, requires Admin or self)
  - [ ] `DELETE /api/users/{id}` (soft delete, requires Admin)
  - [ ] `[Authorize]` attributes on all endpoints
  - [ ] Role-based authorization policies
- [ ] `OrganizationsController.cs`:
  - [ ] `GET /api/organizations` (list hierarchy)
  - [ ] `GET /api/organizations/{id}` (get org details)
  - [ ] `POST /api/organizations` (create org, requires Admin)
  - [ ] `PUT /api/organizations/{id}` (update org, requires Admin)
  - [ ] `DELETE /api/organizations/{id}` (soft delete, requires Admin)
- [ ] `RolesController.cs`:
  - [ ] `GET /api/roles` (list roles)
  - [ ] `POST /api/roles` (create role, requires Admin)
  - [ ] `POST /api/users/{userId}/roles` (assign role, requires Admin)
  - [ ] `DELETE /api/users/{userId}/roles/{roleId}` (remove role, requires Admin)
- [ ] Global exception handling middleware
- [ ] Audit logging middleware (log all requests)
- [ ] Tenant isolation middleware (resolve tenant from JWT)
- [ ] Swagger/OpenAPI UI enabled
- [ ] API versioning configured (v1)

**Deliverable:** REST API controllers

---

### Task 2.7: Unit Tests - Core & Infrastructure
**Priority:** High
**Tags:** phase-1, testing, quality
**Assignee:** Senior .NET Engineer
**Time Estimate:** 12 hours

**Description:**
Write comprehensive unit tests for Core and Infrastructure layers.

**Acceptance Criteria:**
- [ ] Test coverage >80% (verified by coverlet)
- [ ] Unit tests for models (validation, business rules)
- [ ] Unit tests for services:
  - [ ] IdentityService (user CRUD, password validation)
  - [ ] OrganizationService (hierarchy navigation)
  - [ ] RoleService (role assignment)
  - [ ] AuthorizationService (permission evaluation)
- [ ] Mocking strategy (Moq for dependencies)
- [ ] Test naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
- [ ] Arrange-Act-Assert pattern
- [ ] Edge cases tested:
  - [ ] Null inputs
  - [ ] Empty strings
  - [ ] Duplicate entries
  - [ ] Unauthorized access
  - [ ] Tenant isolation violations
- [ ] Tests run in parallel
- [ ] All tests pass on CI/CD

**Deliverable:** Unit test suite (>80% coverage)

---

### Task 2.8: Integration Tests - API
**Priority:** High
**Tags:** phase-1, testing, integration
**Assignee:** Senior .NET Engineer
**Time Estimate:** 12 hours

**Description:**
Write end-to-end integration tests for API endpoints.

**Acceptance Criteria:**
- [ ] WebApplicationFactory setup (in-memory hosting)
- [ ] In-memory PostgreSQL database (Testcontainers)
- [ ] Integration tests for authentication:
  - [ ] Login with valid credentials → 200 + tokens
  - [ ] Login with invalid credentials → 401
  - [ ] Refresh token → 200 + new tokens
  - [ ] Logout → 200 + token revoked
- [ ] Integration tests for users:
  - [ ] Create user → 201
  - [ ] Get user → 200 (authorized)
  - [ ] Get user → 403 (unauthorized)
  - [ ] Update user → 200
  - [ ] Delete user → 204
- [ ] Integration tests for organizations:
  - [ ] Create org → 201
  - [ ] Get org hierarchy → 200
- [ ] Integration tests for roles:
  - [ ] Assign role → 200
  - [ ] User has role → permission granted
- [ ] Test data seeding for each test
- [ ] Database cleanup between tests
- [ ] All tests isolated (no shared state)

**Deliverable:** Integration test suite

---

## Epic 3: Admin UI (Weeks 7-9)

### Task 3.1: React Project Setup
**Priority:** High
**Tags:** phase-1, frontend, setup
**Assignee:** Frontend Contractor
**Time Estimate:** 4 hours

**Description:**
Set up React project with Vite, Tailwind CSS, React Router, TypeScript.

**Acceptance Criteria:**
- [ ] Project created: `C:\Projects\hazina\apps\Web\Hazina.Identity.AdminUI\`
- [ ] Vite + React 18 + TypeScript template
- [ ] Tailwind CSS configured
- [ ] React Router v6 configured
- [ ] Axios for HTTP requests
- [ ] React Query for data fetching
- [ ] Zustand or Context API for state management
- [ ] ESLint + Prettier configured
- [ ] Environment variables (.env files for dev/prod)
- [ ] API base URL configurable
- [ ] Dev server runs on port 5173
- [ ] Builds successfully (`npm run build`)

**Deliverable:** React project scaffold

---

### Task 3.2: Authentication UI
**Priority:** Urgent
**Tags:** phase-1, frontend, auth
**Assignee:** Frontend Contractor
**Time Estimate:** 8 hours

**Description:**
Build login, logout, and protected route components.

**Acceptance Criteria:**
- [ ] Login page (`/login`):
  - [ ] Email input
  - [ ] Password input
  - [ ] "Remember me" checkbox
  - [ ] Submit button
  - [ ] Error messages (invalid credentials)
  - [ ] Loading spinner during login
  - [ ] Redirect to dashboard on success
- [ ] Authentication context:
  - [ ] Store access token in memory
  - [ ] Store refresh token in HTTP-only cookie (or localStorage)
  - [ ] Auto-refresh token before expiry
  - [ ] Logout function (clear tokens, redirect to login)
  - [ ] `useAuth()` hook
- [ ] Protected routes:
  - [ ] Redirect to `/login` if not authenticated
  - [ ] Axios interceptor for JWT token
  - [ ] 401 handling (auto-logout)
- [ ] Top navigation bar:
  - [ ] Current user email
  - [ ] Logout button
- [ ] Responsive design (mobile-friendly)

**Deliverable:** Login UI + auth context

---

### Task 3.3: User Management UI
**Priority:** High
**Tags:** phase-1, frontend, users
**Assignee:** Frontend Contractor
**Time Estimate:** 12 hours

**Description:**
Build user list, create, edit, delete UI.

**Acceptance Criteria:**
- [ ] User list page (`/users`):
  - [ ] Table with columns: Email, Name, Role, Organization, Status, Actions
  - [ ] Pagination (20 users per page)
  - [ ] Search/filter by email or name
  - [ ] Sort by column (email, created date)
  - [ ] "Create User" button
  - [ ] Edit/Delete actions per user
- [ ] Create user modal:
  - [ ] Email input (required, validated)
  - [ ] Name input
  - [ ] Organization dropdown (hierarchical)
  - [ ] Role dropdown (Admin, Manager, Agent, Viewer)
  - [ ] Submit button
  - [ ] Success/error messages
  - [ ] Email invitation sent on creation
- [ ] Edit user modal:
  - [ ] Pre-populated form
  - [ ] Update email, name, organization, role
  - [ ] Save changes
- [ ] Delete user confirmation:
  - [ ] "Are you sure?" modal
  - [ ] Soft delete (user deactivated)
- [ ] Loading states (skeletons during data fetch)
- [ ] Error handling (display API errors)
- [ ] Responsive design

**Deliverable:** User management UI

---

### Task 3.4: Organization Management UI
**Priority:** High
**Tags:** phase-1, frontend, organizations
**Assignee:** Frontend Contractor
**Time Estimate:** 12 hours

**Description:**
Build organization hierarchy view, create, edit, delete UI.

**Acceptance Criteria:**
- [ ] Organization tree view (`/organizations`):
  - [ ] Hierarchical tree component (collapsible nodes)
  - [ ] Display: Organization name, type (Building, Floor, Room)
  - [ ] Expand/collapse children
  - [ ] Create child organization button per node
  - [ ] Edit/Delete actions per node
- [ ] Create organization modal:
  - [ ] Name input (required)
  - [ ] Type dropdown (Agency, Building, Floor, Room)
  - [ ] Parent organization selector (tree picker)
  - [ ] Submit button
  - [ ] Success/error messages
- [ ] Edit organization modal:
  - [ ] Pre-populated form
  - [ ] Update name, type
  - [ ] Cannot change parent (prevent circular references)
- [ ] Delete organization confirmation:
  - [ ] "Are you sure?" modal
  - [ ] Warning if organization has children
  - [ ] Cascade delete option
- [ ] Breadcrumb navigation (show hierarchy path)
- [ ] Responsive design (tree collapses on mobile)

**Deliverable:** Organization management UI

---

### Task 3.5: Role Management UI
**Priority:** Medium
**Tags:** phase-1, frontend, roles
**Assignee:** Frontend Contractor
**Time Estimate:** 8 hours

**Description:**
Build role list, assign/remove roles to users.

**Acceptance Criteria:**
- [ ] Role list page (`/roles`):
  - [ ] Table: Role name, Description, Permissions, Users count
  - [ ] "Create Role" button (Admin only, Phase 1 = default roles only)
  - [ ] View role details (list of permissions)
- [ ] User role assignment (in User edit modal):
  - [ ] Multi-select for roles
  - [ ] Organization-scoped (assign role within org)
  - [ ] Save changes
- [ ] Role badge component:
  - [ ] Display user's roles as colored badges
  - [ ] Used in user list and details
- [ ] Responsive design

**Deliverable:** Role management UI

---

### Task 3.6: Dashboard / Home Page
**Priority:** Medium
**Tags:** phase-1, frontend, dashboard
**Assignee:** Frontend Contractor
**Time Estimate:** 6 hours

**Description:**
Build simple dashboard with stats and quick actions.

**Acceptance Criteria:**
- [ ] Dashboard page (`/` or `/dashboard`):
  - [ ] Stats cards:
    - [ ] Total users
    - [ ] Total organizations
    - [ ] Active sessions (logged-in users)
  - [ ] Recent activity (audit log last 10 entries)
  - [ ] Quick actions:
    - [ ] "Create User" button
    - [ ] "Create Organization" button
  - [ ] Welcome message with current user name
- [ ] Responsive design (cards stack on mobile)
- [ ] Loading states

**Deliverable:** Dashboard UI

---

## Epic 4: Bliek Integration (Weeks 10-11)

### Task 4.1: Add Hazina.Identity to Bliek Backend
**Priority:** Urgent
**Tags:** phase-1, integration, bliek
**Assignee:** You
**Time Estimate:** 6 hours

**Description:**
Integrate Hazina.Identity authentication into Bliek API.

**Acceptance Criteria:**
- [ ] Add NuGet reference to Hazina.Identity.Core (when published)
- [ ] Configure OpenIddict client in Bliek API:
  - [ ] Authority URL (Hazina.Identity.API endpoint)
  - [ ] Client ID, Client Secret (if needed)
  - [ ] Audience, Scopes
- [ ] Add authentication middleware:
  - [ ] `AddAuthentication()` with JwtBearer scheme
  - [ ] Validate JWT issuer, audience, signature
  - [ ] Map JWT claims to User.Identity
- [ ] Add authorization middleware:
  - [ ] `AddAuthorization()` with policies
  - [ ] Role-based policies (Admin, Manager, Agent, Viewer)
- [ ] Add `[Authorize]` attributes to Bliek controllers:
  - [ ] Admin endpoints require "Admin" role
  - [ ] Agent endpoints require "Agent" role
  - [ ] Public endpoints have no attribute
- [ ] Tenant resolution middleware:
  - [ ] Extract tenant ID from JWT "tenant_id" claim
  - [ ] Set current tenant context
  - [ ] Filter all database queries by tenant
- [ ] Test authentication:
  - [ ] Login via Hazina.Identity → get token
  - [ ] Call Bliek API with token → 200
  - [ ] Call Bliek API without token → 401
  - [ ] Call Bliek API with wrong role → 403

**Deliverable:** Bliek backend authenticated via Hazina.Identity

---

### Task 4.2: Add Hazina.Identity to Bliek Frontend
**Priority:** Urgent
**Tags:** phase-1, integration, bliek
**Assignee:** You
**Time Estimate:** 8 hours

**Description:**
Integrate Hazina.Identity authentication into Bliek React frontend.

**Acceptance Criteria:**
- [ ] Remove Bliek's old auth code (if any)
- [ ] Add login redirect to Hazina.Identity.AdminUI
  - OR implement login directly in Bliek UI (call Hazina.Identity.API /auth/login)
- [ ] Store tokens:
  - [ ] Access token in memory (React state)
  - [ ] Refresh token in HTTP-only cookie (set by API)
- [ ] Axios interceptor:
  - [ ] Add "Authorization: Bearer {token}" header to all requests
  - [ ] Handle 401 response (refresh token or redirect to login)
- [ ] Protected routes:
  - [ ] Redirect to login if no token
  - [ ] Fetch current user from `/api/auth/me` on app load
- [ ] Role-based UI rendering:
  - [ ] Show/hide features based on user role
  - [ ] Admin sees "Manage Users" menu
  - [ ] Agent does not see admin features
- [ ] Logout:
  - [ ] Call `/api/auth/logout`
  - [ ] Clear tokens
  - [ ] Redirect to login
- [ ] Test:
  - [ ] Login as Admin → see admin features
  - [ ] Login as Agent → see agent features
  - [ ] Logout → redirected to login

**Deliverable:** Bliek frontend authenticated via Hazina.Identity

---

### Task 4.3: Create Test Users for Bliek
**Priority:** Medium
**Tags:** phase-1, integration, testing
**Assignee:** You
**Time Estimate:** 2 hours

**Description:**
Seed Hazina.Identity database with test users for Bliek.

**Acceptance Criteria:**
- [ ] Create test organization:
  - [ ] Tenant: "Bliek Vastgoed"
  - [ ] Root organization: "Bliek Agency"
  - [ ] Child orgs: "Building A", "Building B"
- [ ] Create test users:
  - [ ] admin@bliek.nl (Admin role, Bliek Agency)
  - [ ] manager@bliek.nl (Manager role, Building A)
  - [ ] agent@bliek.nl (Agent role, Building A)
  - [ ] viewer@bliek.nl (Viewer role, Building A)
- [ ] Password: "Test123!" (same for all, for testing)
- [ ] Document test credentials in README
- [ ] Seed script or migration to auto-create on fresh install

**Deliverable:** Test users in database

---

### Task 4.4: End-to-End Testing - Bliek + Identity
**Priority:** High
**Tags:** phase-1, testing, e2e
**Assignee:** You
**Time Estimate:** 8 hours

**Description:**
Test complete flow: Login → Access Bliek → Perform actions → Logout.

**Acceptance Criteria:**
- [ ] Test scenario: Admin user
  - [ ] Login as admin@bliek.nl
  - [ ] Access Bliek dashboard → 200
  - [ ] Access admin-only endpoint (e.g., delete listing) → 200
  - [ ] Logout → token revoked
  - [ ] Try accessing endpoint after logout → 401
- [ ] Test scenario: Agent user
  - [ ] Login as agent@bliek.nl
  - [ ] Access agent endpoints (view listings) → 200
  - [ ] Try accessing admin endpoint (delete listing) → 403
  - [ ] Logout
- [ ] Test scenario: Unauthenticated user
  - [ ] Try accessing Bliek API without token → 401
  - [ ] Redirected to login page
- [ ] Test scenario: Expired token
  - [ ] Login → get token
  - [ ] Wait for token expiry (or manually expire)
  - [ ] Try accessing API → 401
  - [ ] Auto-refresh token → 200
- [ ] Test scenario: Refresh token
  - [ ] Login → get access + refresh tokens
  - [ ] Call /auth/refresh with refresh token → new access token
  - [ ] Use new token → 200
- [ ] Test multi-tenancy:
  - [ ] User from Tenant A cannot see data from Tenant B
  - [ ] API filters data by tenant automatically
- [ ] Playwright browser tests (optional but recommended)

**Deliverable:** E2E test suite passing

---

## Epic 5: Testing & Launch (Week 12)

### Task 5.1: Security Review
**Priority:** Urgent
**Tags:** phase-1, security, review
**Assignee:** You + Security Expert (if available)
**Time Estimate:** 8 hours

**Description:**
Conduct security review of authentication, authorization, data access.

**Acceptance Criteria:**
- [ ] OWASP Top 10 checklist:
  - [ ] No SQL injection (parameterized queries, EF Core)
  - [ ] No XSS (API doesn't render HTML)
  - [ ] No CSRF (stateless JWT auth)
  - [ ] Secure password storage (BCrypt, salt, >10 rounds)
  - [ ] HTTPS enforced (redirect HTTP to HTTPS)
  - [ ] Secure headers (HSTS, X-Frame-Options, CSP)
  - [ ] Rate limiting on /auth/login (prevent brute force)
  - [ ] JWT expiry enforced (short-lived access tokens)
  - [ ] Refresh token rotation (one-time use)
- [ ] Dependency scan (dotnet list package --vulnerable)
- [ ] Secrets management:
  - [ ] No secrets in appsettings.json (use Azure Key Vault or env vars)
  - [ ] Connection strings in environment variables
  - [ ] JWT signing key secure (X.509 cert in production)
- [ ] Audit logging:
  - [ ] All auth events logged (login, logout, failed login)
  - [ ] All user CRUD logged
  - [ ] All role assignment logged
- [ ] Multi-tenant isolation verified:
  - [ ] Manual test: User A cannot access User B's data (different tenant)
  - [ ] Code review: All queries filtered by TenantId
- [ ] Security documentation:
  - [ ] Threat model document
  - [ ] Security best practices for deployment
  - [ ] Incident response plan (what to do if breach)

**Deliverable:** Security review report + fixes

---

### Task 5.2: Performance Testing
**Priority:** High
**Tags:** phase-1, performance, testing
**Assignee:** You
**Time Estimate:** 6 hours

**Description:**
Test API performance under load, identify bottlenecks.

**Acceptance Criteria:**
- [ ] Load testing with k6 or Apache JMeter:
  - [ ] 100 concurrent users logging in
  - [ ] 1000 requests/sec to /api/users
  - [ ] 500 requests/sec to /api/organizations
- [ ] Performance targets:
  - [ ] /auth/login: <500ms (p95)
  - [ ] /api/users (list): <200ms (p95)
  - [ ] /api/organizations (hierarchy): <300ms (p95)
  - [ ] /auth/refresh: <100ms (p95)
- [ ] Database query optimization:
  - [ ] Indexes on Email, TenantId, ParentId verified
  - [ ] N+1 query issues identified and fixed (use .Include())
  - [ ] Pagination used for large result sets
- [ ] Caching:
  - [ ] Role permissions cached in IMemoryCache
  - [ ] Cache invalidation on role/permission update
  - [ ] Cache hit rate >80%
- [ ] Connection pooling configured (PostgreSQL)
- [ ] APM/observability:
  - [ ] Application Insights or Serilog configured
  - [ ] Log request duration, errors, exceptions
  - [ ] Track slow queries (>1s)

**Deliverable:** Performance test report + optimizations

---

### Task 5.3: Documentation - API Reference
**Priority:** High
**Tags:** phase-1, documentation
**Assignee:** You
**Time Estimate:** 6 hours

**Description:**
Write comprehensive API documentation for developers.

**Acceptance Criteria:**
- [ ] OpenAPI/Swagger UI live (auto-generated)
- [ ] API reference document:
  - [ ] Overview (what is Hazina.Identity)
  - [ ] Authentication flow (login, refresh, logout)
  - [ ] Authorization model (RBAC, roles, permissions)
  - [ ] Endpoint reference (all endpoints documented)
  - [ ] Request/response examples (curl, C#, JavaScript)
  - [ ] Error codes explained (400, 401, 403, 404, 500)
  - [ ] Rate limits documented
  - [ ] Pagination explained (skip, take, total)
- [ ] Integration guide:
  - [ ] How to add Hazina.Identity to an ASP.NET Core app
  - [ ] How to add Hazina.Identity to a React app
  - [ ] How to configure OIDC client
  - [ ] How to protect endpoints with [Authorize]
  - [ ] How to get current user in code
- [ ] Deployment guide:
  - [ ] Prerequisites (PostgreSQL, .NET 9 SDK)
  - [ ] Environment variables
  - [ ] Database migration steps
  - [ ] Docker deployment (Dockerfile, docker-compose.yml)
  - [ ] IIS deployment (if needed)
- [ ] All docs in Markdown in `hazina/docs/identity/`

**Deliverable:** API reference + integration + deployment guides

---

### Task 5.4: Deployment - Staging Environment
**Priority:** Urgent
**Tags:** phase-1, deployment, staging
**Assignee:** You
**Time Estimate:** 8 hours

**Description:**
Deploy Hazina.Identity to staging environment for UAT.

**Acceptance Criteria:**
- [ ] Staging server provisioned:
  - [ ] Option 1: Azure App Service + Azure Database for PostgreSQL
  - [ ] Option 2: Docker on VPS (Digital Ocean, Linode)
  - [ ] Option 3: Local IIS server (if on-premise)
- [ ] Environment configuration:
  - [ ] Connection string to PostgreSQL
  - [ ] JWT signing key (X.509 certificate)
  - [ ] CORS allowed origins (Bliek frontend URL)
  - [ ] HTTPS enforced (SSL certificate)
- [ ] Database migration applied
- [ ] Seed data loaded (default roles, test users)
- [ ] Health check endpoint (`/health`) returns 200
- [ ] Smoke tests:
  - [ ] Login → 200
  - [ ] Get users → 200
  - [ ] Create org → 201
- [ ] Monitoring:
  - [ ] Application logs (Serilog to file or Azure App Insights)
  - [ ] Error tracking (Sentry or Application Insights)
  - [ ] Uptime monitoring (Uptime Robot or Azure Monitor)
- [ ] Staging URL: `https://identity-staging.yourdomain.com`
- [ ] Admin UI accessible
- [ ] Bliek staging can authenticate via staging Identity

**Deliverable:** Staging environment live and tested

---

### Task 5.5: User Acceptance Testing (UAT)
**Priority:** High
**Tags:** phase-1, testing, uat
**Assignee:** You + Bliek Team
**Time Estimate:** 8 hours

**Description:**
Conduct UAT with Bliek team, gather feedback, fix issues.

**Acceptance Criteria:**
- [ ] UAT test plan created:
  - [ ] Login as different roles
  - [ ] Create/edit/delete users
  - [ ] Create/edit/delete organizations
  - [ ] Assign roles to users
  - [ ] Test multi-tenancy (separate data per tenant)
- [ ] UAT participants:
  - [ ] You (as admin)
  - [ ] Bliek team member (as real estate agency admin)
  - [ ] Test user (as agent)
- [ ] UAT sessions:
  - [ ] Session 1: Walkthrough of admin UI
  - [ ] Session 2: Integration with Bliek app
  - [ ] Session 3: Performance and edge cases
- [ ] Feedback collection:
  - [ ] Usability issues
  - [ ] Bugs found
  - [ ] Feature requests (for Phase 2)
- [ ] Bug fixes:
  - [ ] Critical bugs fixed immediately
  - [ ] Medium/low bugs logged for post-launch
- [ ] Sign-off from Bliek team:
  - [ ] "Ready for production" approval

**Deliverable:** UAT report + sign-off

---

### Task 5.6: Production Deployment
**Priority:** Urgent
**Tags:** phase-1, deployment, production
**Assignee:** You
**Time Estimate:** 6 hours

**Description:**
Deploy Hazina.Identity to production, launch to Bliek's first client.

**Acceptance Criteria:**
- [ ] Production server provisioned (same as staging but production-grade)
- [ ] Database backed up before deployment
- [ ] Deploy steps:
  - [ ] Build release binaries (`dotnet publish -c Release`)
  - [ ] Upload to server (FTP, SCP, Azure Deploy)
  - [ ] Run database migrations
  - [ ] Seed default roles
  - [ ] Restart API service
- [ ] Smoke tests in production:
  - [ ] Login → 200
  - [ ] Get users → 200
  - [ ] Create org → 201
- [ ] Monitoring configured:
  - [ ] Alerts on errors (email/Slack)
  - [ ] Uptime monitoring
  - [ ] Performance dashboard
- [ ] Production URL: `https://identity.yourdomain.com`
- [ ] Admin UI accessible
- [ ] Bliek production app authenticated via production Identity
- [ ] First customer onboarded:
  - [ ] Create tenant for customer
  - [ ] Create admin user for customer
  - [ ] Customer can login and manage their organization
- [ ] Launch announcement:
  - [ ] Internal team announcement
  - [ ] Bliek team notification
  - [ ] Customer notification (welcome email)

**Deliverable:** Hazina.Identity live in production

---

## Post-Launch Tasks (Optional, but recommended)

### Task 6.1: Analytics & Metrics
**Priority:** Medium
**Tags:** phase-1, analytics, post-launch
**Assignee:** You
**Time Estimate:** 4 hours

**Description:**
Set up analytics to track usage, performance, errors.

**Acceptance Criteria:**
- [ ] Metrics to track:
  - [ ] Daily active users (DAU)
  - [ ] Successful logins per day
  - [ ] Failed logins per day
  - [ ] API requests per endpoint
  - [ ] Average response time
  - [ ] Error rate (5xx responses)
- [ ] Dashboard:
  - [ ] Grafana + Prometheus
  - [ ] OR Application Insights dashboard
  - [ ] OR custom SQL queries + simple HTML page
- [ ] Alerts:
  - [ ] Error rate >5% → email alert
  - [ ] Response time >1s → Slack alert
  - [ ] Failed logins >100/hour → SMS alert (potential attack)

**Deliverable:** Analytics dashboard

---

### Task 6.2: Feedback Collection
**Priority:** Medium
**Tags:** phase-1, feedback, post-launch
**Assignee:** You
**Time Estimate:** 2 hours

**Description:**
Gather feedback from Bliek and first customer.

**Acceptance Criteria:**
- [ ] Feedback survey:
  - [ ] "How easy was it to set up users and organizations?" (1-10)
  - [ ] "Any features missing for Phase 1?"
  - [ ] "Any bugs or issues encountered?"
  - [ ] "Would you recommend this to other real estate agencies?"
- [ ] Customer interviews (30 min each):
  - [ ] Bliek team
  - [ ] First customer admin
  - [ ] End users (agents)
- [ ] Feedback analysis:
  - [ ] Top requested features for Phase 2
  - [ ] Common pain points
  - [ ] Success metrics (NPS score)

**Deliverable:** Feedback report + Phase 2 feature prioritization

---

## Summary

**Total Tasks:** 38 tasks (6 epics)
**Total Estimated Time:** ~250 hours (12 weeks @ 20-25 hrs/week for 2 people)
**Team:** You + 1 senior .NET engineer + 1 frontend contractor

**Epic Breakdown:**
1. **Architecture & Setup (Weeks 1-2):** 5 tasks, 38 hours
2. **Core Implementation (Weeks 3-6):** 8 tasks, 98 hours
3. **Admin UI (Weeks 7-9):** 6 tasks, 50 hours
4. **Bliek Integration (Weeks 10-11):** 4 tasks, 24 hours
5. **Testing & Launch (Week 12):** 6 tasks, 42 hours
6. **Post-Launch (Optional):** 2 tasks, 6 hours

**Phases:**
- **Phase 1 (MVP):** All tasks above → Bliek launches with working IAM
- **Phase 2 (Smart Building Vertical):** To be planned after Phase 1 completion
- **Phase 3 (Enterprise IAM):** To be planned after Phase 2 completion

**Success Criteria:**
- ✅ Bliek integrated and using in production
- ✅ 1 paying customer (Bliek or their client)
- ✅ 0 critical security vulnerabilities
- ✅ <200ms API response time (p95)
- ✅ >80% test coverage

---

## Next Steps

1. **Review this task breakdown** - Does scope look correct?
2. **Adjust timeline if needed** - Is 12 weeks realistic?
3. **Create tasks in ClickUp board** - Manually or via bulk import
4. **Assign ownership** - You vs Senior Engineer vs Contractor
5. **Hire senior .NET engineer ASAP** - Critical blocker
6. **Start Week 1: Architecture design**

---

**ClickUp Board:** https://app.clickup.com/9012956001/v/b/li/901216517140
**Roadmap Document:** C:\Projects\hazina\docs\identity\HAZINA_IDENTITY_ROADMAP.md
**This Task List:** C:\Projects\hazina\docs\identity\CLICKUP_TASKS_PHASE1.md

**Ready to build the IAM platform Microsoft wishes they hadn't let die.**
