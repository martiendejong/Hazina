# Hazina.Identity - Enterprise IAM Platform

## Executive Summary

**Vision:** Build the world's most advanced Identity and Access Management (IAM) platform, specializing in smart buildings and IoT device authorization, competing with Auth0, Okta, and Microsoft in the $33-67 billion IAM market.

**Background:** Leveraging expertise from BHold Controls (acquired by Microsoft 2011, integrated into Forefront Identity Manager), this project resurrects the vision Microsoft abandoned and extends it to modern IoT, AI agents, and multi-tenant SaaS architectures.

**Market Positioning:**
- **Niche dominance:** Smart buildings + IoT authorization (underserved)
- **Competitive advantage:** Physical + digital access convergence
- **Business model:** Open-source core + commercial enterprise features
- **First customer:** Bliek Vastgoed (real estate agency platform)

**Timeline:**
- **Phase 1 (Months 1-3):** MVP for Bliek - Basic RBAC + multi-tenant
- **Phase 2 (Months 4-12):** Smart Building Vertical - IoT + SaaS platform
- **Phase 3 (Months 13-24):** Enterprise IAM - General-purpose, compete with Auth0

---

## Architecture Overview

### Hazina Repository Structure

```
C:\Projects\hazina\
├── src\
│   ├── Identity\                              # NEW - Identity & Access Management
│   │   ├── Hazina.Identity.Core\              # Open-source core (Apache 2.0)
│   │   │   ├── Abstractions\
│   │   │   │   ├── IIdentityService.cs
│   │   │   │   ├── IOrganizationService.cs
│   │   │   │   ├── IRoleService.cs
│   │   │   │   ├── IPolicyEngine.cs
│   │   │   │   └── IAuthorizationService.cs
│   │   │   ├── Models\
│   │   │   │   ├── HazinaUser.cs
│   │   │   │   ├── Organization.cs
│   │   │   │   ├── Role.cs
│   │   │   │   ├── Permission.cs
│   │   │   │   ├── Policy.cs
│   │   │   │   └── Resource.cs              # Building/Floor/Room/Device
│   │   │   ├── Authorization\
│   │   │   │   ├── RBAC\                     # Role-Based Access Control
│   │   │   │   ├── ABAC\                     # Attribute-Based Access Control
│   │   │   │   ├── ReBAC\                    # Relationship-Based Access Control
│   │   │   │   └── PolicyEngine.cs           # Unified policy evaluation
│   │   │   └── DependencyInjection.cs
│   │   │
│   │   ├── Hazina.Identity.Infrastructure\    # Data access, repositories
│   │   │   ├── Data\
│   │   │   │   ├── IdentityDbContext.cs
│   │   │   │   └── Migrations\
│   │   │   ├── Repositories\
│   │   │   │   ├── UserRepository.cs
│   │   │   │   ├── OrganizationRepository.cs
│   │   │   │   └── RoleRepository.cs
│   │   │   └── Services\
│   │   │       ├── IdentityService.cs
│   │   │       ├── OrganizationService.cs
│   │   │       └── AuthorizationService.cs
│   │   │
│   │   ├── Hazina.Identity.OIDC\              # OpenID Connect provider
│   │   │   ├── OpenIddictConfiguration.cs     # Using OpenIddict
│   │   │   ├── TokenService.cs
│   │   │   └── ClaimsTransformation.cs
│   │   │
│   │   ├── Hazina.Identity.API\               # Identity API service
│   │   │   ├── Controllers\
│   │   │   │   ├── UsersController.cs
│   │   │   │   ├── OrganizationsController.cs
│   │   │   │   ├── RolesController.cs
│   │   │   │   ├── PoliciesController.cs
│   │   │   │   └── AuthController.cs
│   │   │   ├── Program.cs
│   │   │   └── appsettings.json
│   │   │
│   │   ├── Hazina.Identity.IoT\               # Phase 2 - IoT device integration
│   │   │   ├── Protocols\
│   │   │   │   ├── MQTT\
│   │   │   │   ├── CoAP\
│   │   │   │   └── OPC_UA\
│   │   │   ├── DeviceRegistry.cs
│   │   │   ├── DeviceAuthorization.cs
│   │   │   └── StreamingAuthorization.cs      # Real-time event authorization
│   │   │
│   │   ├── Hazina.Identity.Federation\        # Phase 3 - AD/LDAP integration
│   │   │   ├── ActiveDirectory\
│   │   │   ├── LDAP\
│   │   │   └── SAML\
│   │   │
│   │   └── Hazina.Identity.Enterprise\        # Phase 3 - Commercial features
│   │       ├── MultiTenancy\
│   │       ├── HighAvailability\
│   │       └── AuditCompliance\
│   │
│   ├── Apps\
│   │   └── Web\
│   │       └── Hazina.Identity.AdminUI\       # React admin dashboard
│   │
│   └── Tests\
│       └── Identity\
│           ├── Hazina.Identity.Core.Tests\
│           ├── Hazina.Identity.API.Tests\
│           └── Hazina.Identity.Integration.Tests\
│
├── docs\
│   └── identity\
│       ├── architecture.md
│       ├── authorization-models.md            # RBAC + ABAC + ReBAC explained
│       ├── smart-building-use-cases.md
│       ├── api-reference.md
│       └── deployment-guide.md
│
└── examples\
    └── 06-identity-sso\
        ├── BliekIntegration\                  # Example: Bliek using Hazina.Identity
        └── SmartBuildingDemo\                 # Example: Building access control
```

---

## Authorization Model Design

### Three Authorization Models (Unified)

#### 1. RBAC (Role-Based Access Control)
```csharp
// Roles in the system
public enum SystemRole
{
    // Building ownership
    BuildingOwner,
    BuildingManager,

    // Staff
    SecurityAdmin,
    FacilityManager,
    Receptionist,

    // Contractors
    HVACTechnician,
    CleaningStaff,
    SecurityGuard,

    // Tenants
    TenantAdmin,
    TenantEmployee,

    // System
    SystemAdmin,
    Auditor
}
```

#### 2. ABAC (Attribute-Based Access Control)
```csharp
// Attributes for authorization decisions
public class AccessContext
{
    public DateTime RequestTime { get; set; }
    public GeoLocation Location { get; set; }
    public DeviceType DeviceType { get; set; }
    public NetworkType Network { get; set; }
    public RiskScore RiskScore { get; set; }
    public string IPAddress { get; set; }
    public bool IsEmergency { get; set; }
}

// Example policy:
// "HVAC Technician can access HVAC systems on assigned floors
//  during business hours (8am-6pm) from company network"
```

#### 3. ReBAC (Relationship-Based Access Control)
```csharp
// Relationship hierarchy
Organization (BuildingPortfolio Inc.)
  └── Building (123 Main Street)
      └── Floor (Floor 5)
          └── Room (Room 503)
              └── Device (HVAC Controller #42)

// Relationship rules:
// - User assigned to Organization → inherits access to all Buildings
// - User assigned to Building → inherits access to all Floors
// - User assigned to Floor → inherits access to all Rooms
// - User assigned to Room → inherits access to all Devices

// Delegation:
// - Building Manager can delegate Floor access to Facility Manager
// - Facility Manager can delegate Room access to Technician
```

### Unified Policy Engine

All three models are evaluated together:

```
Authorization Decision =
  RBAC (Does user have role?)
  AND ABAC (Do attributes match policy?)
  AND ReBAC (Does relationship grant access?)

Example:
  User: John (HVACTechnician)
  Requesting: Access to HVAC Controller #42 in Room 503

  RBAC: ✓ John has HVACTechnician role
  ABAC: ✓ Current time is 10am (business hours)
        ✓ Location is company network
        ✓ Device type matches role permissions
  ReBAC: ✓ John is assigned to Floor 5
         ✓ Room 503 is on Floor 5
         ✓ HVAC Controller #42 is in Room 503

  RESULT: ALLOW
```

---

## Phase 1: MVP for Bliek (Months 1-3)

### Goal
Working IAM that Bliek can use for real estate agency clients.

### Scope (Ruthlessly Minimal)

**Features:**
- [x] User management (CRUD users, invite, deactivate)
- [x] Organization hierarchy (Agency → Building → Floor → Room)
- [x] Role-based access (Admin, Manager, Agent, Viewer)
- [x] JWT authentication (login, refresh tokens, logout)
- [x] Basic authorization (role-based endpoint protection)
- [x] Audit logging (who did what, when)
- [x] Admin UI (manage users, roles, organizations)

**NOT in Phase 1:**
- ❌ IoT devices
- ❌ Streaming data authorization
- ❌ Active Directory integration
- ❌ Multi-tenant SaaS (single tenant for Bliek initially)
- ❌ On-premise deployment
- ❌ AI agent authorization
- ❌ ABAC/ReBAC (RBAC only)

### Technical Stack

- **Backend:** ASP.NET Core 9.0, OpenIddict (OIDC), PostgreSQL
- **Frontend:** React 18, Vite, Tailwind CSS
- **Authentication:** JWT tokens, HTTP-only cookies
- **Authorization:** Role-based (RBAC)
- **Database:** PostgreSQL with EF Core migrations

### Deliverables

1. **Hazina.Identity.Core** - Core abstractions and models
2. **Hazina.Identity.Infrastructure** - Data access layer
3. **Hazina.Identity.OIDC** - OpenID Connect provider
4. **Hazina.Identity.API** - REST API service
5. **Hazina.Identity.AdminUI** - React admin dashboard
6. **Bliek Integration** - Bliek API using Hazina.Identity
7. **Documentation** - API reference, integration guide
8. **Tests** - Unit tests, integration tests (>80% coverage)

### Success Criteria

- ✅ Bliek frontend can authenticate users via Hazina.Identity
- ✅ Bliek API endpoints are protected by role-based authorization
- ✅ Admin can create organizations, users, assign roles
- ✅ Audit log tracks all identity operations
- ✅ 1 real estate agency (Bliek or client) using it in production

### Timeline (12 weeks)

**Weeks 1-2: Architecture & Setup**
- Architecture design document
- Repository structure creation
- Database schema design
- API contract definition
- Technology evaluation (OpenIddict vs Duende)

**Weeks 3-6: Core Implementation**
- Hazina.Identity.Core (models, abstractions)
- Hazina.Identity.Infrastructure (repositories, services)
- Hazina.Identity.OIDC (token issuance, validation)
- Hazina.Identity.API (users, organizations, roles endpoints)
- Database migrations
- Unit tests

**Weeks 7-9: Admin UI**
- React project setup
- User management UI
- Organization management UI
- Role assignment UI
- Login/logout flows

**Weeks 10-11: Bliek Integration**
- Add Hazina.Identity package to Bliek
- Configure OIDC authentication
- Add [Authorize] attributes to endpoints
- Frontend token management
- Protected routes

**Week 12: Testing & Launch**
- Integration testing
- Security review
- Performance testing
- Documentation
- Deploy to Bliek staging
- First customer onboarding

---

## Phase 2: Smart Building Vertical (Months 4-12)

### Goal
Product that 100 real estate agencies would pay for. Become the #1 IAM for smart buildings.

### New Features

**IoT Device Authorization:**
- Device registry (register/deregister devices)
- Device authentication (mutual TLS, device certificates)
- Real-time authorization (streaming telemetry, <50ms latency)
- Protocol support: MQTT, CoAP, OPC UA
- Device policies (location-based, time-based, role-based)

**Multi-Tenant SaaS:**
- Tenant isolation (data, users, policies)
- Tenant provisioning (self-service signup)
- Tenant administration
- Cross-tenant resource sharing (optional)

**Physical Access Control:**
- Badge reader integration (HID, SALTO, Honeywell)
- Mobile credentials (iOS/Android apps)
- Biometric authentication (fingerprint, face recognition)
- Door controller integration
- Access schedules (time-based unlocking)

**Contractor Workflows:**
- Temporary access grants (expiration, revocation)
- Access delegation (manager delegates to technician)
- Work order integration (access tied to service requests)
- Visitor management (pre-registration, QR codes)

**Advanced Authorization:**
- ABAC implementation (time, location, device type)
- ReBAC implementation (building hierarchy relationships)
- Policy engine (Open Policy Agent integration)
- Emergency access overrides

**Compliance & Audit:**
- Comprehensive audit trails
- GDPR compliance (right to erasure, data portability)
- Access certification (periodic review)
- Separation of duties enforcement

### Deliverables

1. **Hazina.Identity.IoT** - IoT device integration
2. **Hazina.Identity.Enterprise** - Multi-tenancy
3. **Mobile apps** - iOS/Android for physical access
4. **SaaS platform** - identity.hazina.ai
5. **10 paying customers** - Real estate agencies
6. **$600K ARR** - $50K MRR revenue target

### Success Criteria

- ✅ 100K+ IoT devices authorized in real-time
- ✅ 10 real estate agencies paying $500-5K/month
- ✅ Mobile credentials working for door access
- ✅ Case studies published
- ✅ Word-of-mouth growth

---

## Phase 3: Enterprise IAM Platform (Months 13-24)

### Goal
General-purpose IAM competing with Auth0, Okta, Microsoft.

### New Features

**On-Premise Deployment:**
- Kubernetes Helm charts
- Docker Compose
- Windows MSI installer
- Air-gapped installation support
- HA/DR architecture

**Active Directory / LDAP:**
- AD user federation
- LDAP synchronization
- Group mapping (AD groups → roles)
- Hybrid cloud + on-premise

**AI Agent Authorization:**
- LLM agent identity (bot accounts)
- Fine-grained API permissions for agents
- Agent delegation (human delegates to AI)
- Agent audit trails (what did AI access?)

**Enterprise Features:**
- Single Sign-On (SSO) integrations
- SAML 2.0 support
- OAuth2 provider
- MFA (TOTP, SMS, hardware tokens)
- Passwordless authentication
- 99.99% SLA
- 24/7 support

### Deliverables

1. **Hazina.Identity.Federation** - AD/LDAP integration
2. **On-premise installer** - MSI, Docker, Kubernetes
3. **Enterprise sales team** - Hire sales, support
4. **100 customers** - Across multiple verticals
5. **$2.4M ARR** - $200K MRR revenue target

---

## Competitive Analysis

### vs Auth0

**Auth0 Weaknesses:**
- Cloud-only (no on-premise)
- Expensive ($240/month for 1000 users → $23K+/year enterprise)
- Generic (not optimized for buildings/IoT)
- No physical access control integration

**Our Advantages:**
- Hybrid (SaaS + on-premise)
- 10x cheaper (freemium + open-source core)
- Smart building specialist
- Physical + digital unified

### vs Microsoft Azure AD

**Microsoft Weaknesses:**
- BHold deprecated, MIM dying
- Complex enterprise sales
- Cloud-first (on-premise weak)
- Not IoT-native

**Our Advantages:**
- BHold resurrection narrative
- Simpler, developer-friendly
- IoT-first design
- Building-native UX

### vs Okta

**Okta Weaknesses:**
- $2.9B revenue = expensive enterprise pricing
- Cloud-only
- Generic horizontal IAM
- Just announced AI agent support (we can beat them to market)

**Our Advantages:**
- Open-source core (can't compete with free)
- Vertical focus (buildings)
- AI agent auth built-in from day 1
- IoT device authorization

---

## Business Model

### Open-Source Core + Commercial Enterprise

**Hazina.Identity.Core (Apache 2.0 - Free):**
- User management
- Role-based authorization (RBAC)
- JWT authentication
- Basic audit logging
- API integrations
- Community support

**Hazina.Identity.Enterprise (Commercial - Paid):**
- Multi-tenancy
- IoT device authorization
- On-premise deployment
- Active Directory / LDAP
- High availability / disaster recovery
- 99.99% SLA
- 24/7 support
- Professional services

### Pricing (Phase 2+)

**SaaS Tiers:**
- **Free:** Up to 100 users, community support
- **Starter:** $99/month - Up to 1,000 users, 100 devices, email support
- **Professional:** $499/month - Up to 10,000 users, 1,000 devices, IoT auth, phone support
- **Enterprise:** $2,500+/month - Unlimited, on-premise option, AD/LDAP, SLA, dedicated support

**Real Estate Agency Pricing:**
- $5/building/month (1-10 buildings)
- $3/building/month (11-100 buildings)
- Custom pricing for 100+ building portfolios

### Revenue Projections

**Phase 1 (Months 1-3):**
- Customers: 1 (Bliek or pilot)
- MRR: $0-500
- ARR: $0-6K

**Phase 2 (Months 4-12):**
- Customers: 10 real estate agencies
- MRR: $50K
- ARR: $600K

**Phase 3 (Months 13-24):**
- Customers: 100 (real estate + other verticals)
- MRR: $200K
- ARR: $2.4M

---

## Go-To-Market Strategy

### Phase 1: Product-Led Growth (Bliek Channel)

- Bliek launches with Hazina.Identity
- Bliek's real estate agency clients become our customers
- Revenue share: Bliek gets 20%, we get 80%
- Case study: "How Bliek provides enterprise IAM to small agencies"

### Phase 2: Vertical SaaS (Smart Buildings)

- Content marketing: "IAM for smart buildings" SEO
- Direct outreach: Real estate conferences, facility management shows
- Partnership: HID, SALTO, Honeywell (physical access vendors)
- Freemium: Free tier for <100 users drives adoption

### Phase 3: Horizontal Expansion

- Open-source community: GitHub, NuGet packages
- Developer advocacy: Conference talks, blog posts, tutorials
- Enterprise sales: Hire sales team, target Fortune 500
- Channel partners: System integrators, consultants

---

## Team & Hiring

### Phase 1 (Months 1-3)
- **You:** Architect, lead developer, product owner
- **Senior .NET Engineer (hire ASAP):** Backend development
- **Contractors (as needed):** Frontend React developer, QA tester

### Phase 2 (Months 4-12)
- **Add:**
  - Backend Engineer #2 (IoT specialist)
  - Frontend Engineer (React, mobile)
  - DevOps Engineer (Kubernetes, SaaS infrastructure)
  - Customer Success (support, onboarding)

### Phase 3 (Months 13-24)
- **Add:**
  - Sales Director
  - Account Executives (2-3)
  - Solutions Architect
  - Technical Writer

---

## Risk Mitigation

### Top Risks

1. **Scope Creep ("Boil the Ocean")**
   - **Mitigation:** Ruthless Phase 1 scope, say NO to features not needed by Bliek

2. **Bliek Can't Wait (IAM delays their launch)**
   - **Mitigation:** Parallel track - basic auth for Bliek NOW, migrate to full IAM later

3. **Team Scaling (Solo can't build enterprise product)**
   - **Mitigation:** Hire senior .NET engineer in Week 1, contractors for frontend

4. **Market Timing (2 years is too slow)**
   - **Mitigation:** Phase 1 in 3 months, revenue by Month 4, iterate fast

5. **Microsoft Competitive Response**
   - **Mitigation:** Own smart building niche they won't defend, move fast

6. **Funding Runway**
   - **Mitigation:** Revenue-first - charge Bliek clients immediately, bootstrap to profitability

---

## Success Metrics

### Phase 1 (MVP)
- ✅ Bliek integrated and using in production
- ✅ 1 paying customer (Bliek or their client)
- ✅ 0 critical security vulnerabilities
- ✅ <200ms API response time (p95)
- ✅ >80% test coverage

### Phase 2 (Vertical)
- ✅ 10 paying customers
- ✅ $50K MRR ($600K ARR)
- ✅ 100K+ IoT devices authorized
- ✅ NPS score >50
- ✅ <50ms device authorization latency

### Phase 3 (Enterprise)
- ✅ 100 paying customers
- ✅ $200K MRR ($2.4M ARR)
- ✅ 5 enterprise deals >$50K/year
- ✅ 99.99% uptime SLA achieved
- ✅ Series A funding OR profitability

---

## Next Steps (Immediate Action Items)

### This Week
1. ✅ **Create ClickUp board** - https://app.clickup.com/9012956001/v/b/li/901216517140
2. ✅ **This strategic roadmap document**
3. ⬜ **Review and approve roadmap** (USER ACTION REQUIRED)
4. ⬜ **Create Phase 1 task breakdown in ClickUp**
5. ⬜ **Hire senior .NET engineer** (job posting, interview pipeline)

### Next Week
1. ⬜ **Architecture design session** (whiteboard organization hierarchy, API contracts)
2. ⬜ **Technology decisions** (OpenIddict vs Duende, confirm PostgreSQL)
3. ⬜ **Repository setup** (create Hazina Identity folders, initial .csproj files)
4. ⬜ **Database schema design** (ERD for users, organizations, roles, resources)
5. ⬜ **Bliek requirements workshop** (what exactly does Bliek need in Phase 1?)

### Month 1
1. ⬜ **Hazina.Identity.Core** implementation
2. ⬜ **Hazina.Identity.Infrastructure** implementation
3. ⬜ **Hazina.Identity.OIDC** setup
4. ⬜ **Database migrations**
5. ⬜ **Unit tests** (>80% coverage)

---

## Questions for User

Before proceeding, please clarify:

1. **Timeline:** Is 3 months for Phase 1 MVP acceptable, or does Bliek need auth faster?
   - If faster needed: Build basic JWT auth for Bliek first, then extract to Hazina.Identity

2. **Budget:** Can we hire a senior .NET engineer for Phase 1? (Budget: $80-120K/year or contractor $100-150/hr)

3. **Bliek Integration:** Does Bliek need IAM for launch, or can they launch with basic auth first?

4. **Business Model:** Comfortable with open-source core + commercial enterprise features?

5. **First Customer:** Is Bliek itself the customer, or one of Bliek's real estate agency clients?

6. **Scope:** Agree Phase 1 should be RBAC only (not ABAC/ReBAC/IoT)? Or must-have features?

---

## Conclusion

This is not "SSO for 10 apps" - this is **building a $50M+ enterprise IAM business** competing with Auth0, Okta, and Microsoft.

**The opportunity is real:**
- $33-67B market growing 15%/year
- You have BHold credibility
- Smart building niche is underserved
- Bliek provides first customer and revenue

**The strategy is sound:**
- Phase 1: Prove it works (Bliek MVP)
- Phase 2: Own the niche (smart buildings)
- Phase 3: Go horizontal (enterprise IAM)

**The risks are manageable:**
- Ruthless scoping prevents "boil ocean"
- Revenue-first prevents funding crunch
- Vertical focus avoids head-on competition

**The path is clear:**
- Build Hazina.Identity as framework module
- Use Bliek as validation customer
- Open-source core, commercial enterprise
- Grow to $2.4M ARR in 24 months

**You've built an enterprise IAM platform before (BHold).** This time, you own it.

**Ready to proceed?**

---

**ClickUp Board:** https://app.clickup.com/9012956001/v/b/li/901216517140
**Repository Path:** C:\Projects\hazina\src\Identity\
**First Commit:** Architecture design document

**Let's build the IAM platform Microsoft wishes they hadn't let die.**
