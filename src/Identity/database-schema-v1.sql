-- Hazina.Identity Database Schema v1.0
-- PostgreSQL 14+
-- Phase 1: MVP for Bliek

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ============================================================================
-- TENANTS & ORGANIZATIONS
-- ============================================================================

-- Multi-tenant isolation: Each customer (e.g., Bliek, or Bliek's clients) is a tenant
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(255) NOT NULL,
    slug VARCHAR(100) NOT NULL UNIQUE, -- URL-safe identifier (e.g., 'bliek-vastgoed')
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP NULL -- Soft delete
);

CREATE INDEX idx_tenants_slug ON tenants(slug) WHERE deleted_at IS NULL;
CREATE INDEX idx_tenants_active ON tenants(is_active) WHERE deleted_at IS NULL;

-- Organization hierarchy: Agency → Building → Floor → Room
CREATE TABLE organizations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    parent_id UUID NULL REFERENCES organizations(id) ON DELETE CASCADE, -- NULL = root organization
    name VARCHAR(255) NOT NULL,
    type VARCHAR(50) NOT NULL, -- 'agency', 'building', 'floor', 'room', 'device'
    metadata JSONB NULL, -- Flexible storage for custom attributes (address, lat/lng, etc.)
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP NULL
);

CREATE INDEX idx_organizations_tenant ON organizations(tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_organizations_parent ON organizations(parent_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_organizations_type ON organizations(type) WHERE deleted_at IS NULL;

-- ============================================================================
-- USERS & AUTHENTICATION
-- ============================================================================

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    email VARCHAR(255) NOT NULL,
    email_verified BOOLEAN NOT NULL DEFAULT false,
    password_hash VARCHAR(255) NOT NULL, -- BCrypt hash
    first_name VARCHAR(100) NULL,
    last_name VARCHAR(100) NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    last_login_at TIMESTAMP NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMP NULL,
    CONSTRAINT uq_users_email_tenant UNIQUE (email, tenant_id)
);

CREATE INDEX idx_users_tenant ON users(tenant_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_users_email ON users(email) WHERE deleted_at IS NULL;
CREATE INDEX idx_users_active ON users(is_active) WHERE deleted_at IS NULL;

-- Refresh tokens for JWT authentication
CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token VARCHAR(500) NOT NULL UNIQUE, -- Hashed refresh token
    expires_at TIMESTAMP NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMP NULL
);

CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token) WHERE is_revoked = false;
CREATE INDEX idx_refresh_tokens_expires ON refresh_tokens(expires_at) WHERE is_revoked = false;

-- ============================================================================
-- ROLES & PERMISSIONS (RBAC)
-- ============================================================================

-- System-wide roles (not tenant-specific)
CREATE TABLE roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(100) NOT NULL UNIQUE, -- 'Admin', 'Manager', 'Agent', 'Viewer'
    description TEXT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Seed default roles
INSERT INTO roles (id, name, description) VALUES
    ('00000000-0000-0000-0000-000000000001', 'Admin', 'Full system access, can manage users and organizations'),
    ('00000000-0000-0000-0000-000000000002', 'Manager', 'Can manage their organization and sub-organizations'),
    ('00000000-0000-0000-0000-000000000003', 'Agent', 'Can view and edit resources within their organization'),
    ('00000000-0000-0000-0000-000000000004', 'Viewer', 'Read-only access to their organization');

-- Permissions: What actions can be performed on what resources
CREATE TABLE permissions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    resource VARCHAR(100) NOT NULL, -- 'users', 'organizations', 'buildings', 'devices'
    action VARCHAR(50) NOT NULL, -- 'create', 'read', 'update', 'delete', 'list'
    description TEXT NULL,
    CONSTRAINT uq_permissions_resource_action UNIQUE (resource, action)
);

-- Seed default permissions
INSERT INTO permissions (resource, action, description) VALUES
    ('users', 'create', 'Create new users'),
    ('users', 'read', 'View user details'),
    ('users', 'update', 'Update user information'),
    ('users', 'delete', 'Delete users'),
    ('users', 'list', 'List all users'),
    ('organizations', 'create', 'Create new organizations'),
    ('organizations', 'read', 'View organization details'),
    ('organizations', 'update', 'Update organization information'),
    ('organizations', 'delete', 'Delete organizations'),
    ('organizations', 'list', 'List all organizations'),
    ('roles', 'assign', 'Assign roles to users'),
    ('roles', 'revoke', 'Revoke roles from users');

-- Role-Permission mapping: Which roles have which permissions
CREATE TABLE role_permissions (
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id UUID NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- Seed Admin role permissions (all permissions)
INSERT INTO role_permissions (role_id, permission_id)
SELECT '00000000-0000-0000-0000-000000000001', id FROM permissions;

-- Seed Manager role permissions (read + update organizations/users in their scope)
INSERT INTO role_permissions (role_id, permission_id)
SELECT '00000000-0000-0000-0000-000000000002', id FROM permissions
WHERE (resource = 'organizations' AND action IN ('read', 'update', 'list'))
   OR (resource = 'users' AND action IN ('read', 'list'));

-- Seed Agent role permissions (read organizations/users)
INSERT INTO role_permissions (role_id, permission_id)
SELECT '00000000-0000-0000-0000-000000000003', id FROM permissions
WHERE action = 'read' OR action = 'list';

-- Seed Viewer role permissions (read only)
INSERT INTO role_permissions (role_id, permission_id)
SELECT '00000000-0000-0000-0000-000000000004', id FROM permissions
WHERE action = 'read' OR action = 'list';

-- User-Role assignments: Users have roles within specific organizations
CREATE TABLE user_roles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    role_id UUID NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    organization_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE, -- Role is scoped to org
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_user_role_org UNIQUE (user_id, role_id, organization_id)
);

CREATE INDEX idx_user_roles_user ON user_roles(user_id);
CREATE INDEX idx_user_roles_role ON user_roles(role_id);
CREATE INDEX idx_user_roles_org ON user_roles(organization_id);

-- ============================================================================
-- AUDIT LOGS
-- ============================================================================

CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id UUID NULL REFERENCES users(id) ON DELETE SET NULL, -- NULL if system action
    action VARCHAR(100) NOT NULL, -- 'user.created', 'user.login', 'role.assigned', etc.
    resource_type VARCHAR(100) NULL, -- 'user', 'organization', 'role'
    resource_id UUID NULL, -- ID of the affected resource
    details JSONB NULL, -- Flexible storage for action details
    ip_address INET NULL,
    user_agent TEXT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_tenant ON audit_logs(tenant_id);
CREATE INDEX idx_audit_logs_user ON audit_logs(user_id);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_created ON audit_logs(created_at DESC);

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

-- Function to get organization hierarchy (all ancestors)
CREATE OR REPLACE FUNCTION get_organization_ancestors(org_id UUID)
RETURNS TABLE (id UUID, name VARCHAR, type VARCHAR, level INT) AS $$
WITH RECURSIVE ancestors AS (
    -- Base case: the organization itself
    SELECT o.id, o.name, o.type, o.parent_id, 0 AS level
    FROM organizations o
    WHERE o.id = org_id AND o.deleted_at IS NULL

    UNION ALL

    -- Recursive case: parent organizations
    SELECT o.id, o.name, o.type, o.parent_id, a.level + 1
    FROM organizations o
    INNER JOIN ancestors a ON o.id = a.parent_id
    WHERE o.deleted_at IS NULL
)
SELECT id, name, type, level FROM ancestors ORDER BY level;
$$ LANGUAGE SQL;

-- Function to get organization descendants (all children)
CREATE OR REPLACE FUNCTION get_organization_descendants(org_id UUID)
RETURNS TABLE (id UUID, name VARCHAR, type VARCHAR, level INT) AS $$
WITH RECURSIVE descendants AS (
    -- Base case: the organization itself
    SELECT o.id, o.name, o.type, o.parent_id, 0 AS level
    FROM organizations o
    WHERE o.id = org_id AND o.deleted_at IS NULL

    UNION ALL

    -- Recursive case: child organizations
    SELECT o.id, o.name, o.type, o.parent_id, d.level + 1
    FROM organizations o
    INNER JOIN descendants d ON o.parent_id = d.id
    WHERE o.deleted_at IS NULL
)
SELECT id, name, type, level FROM descendants ORDER BY level;
$$ LANGUAGE SQL;

-- Function to check if user has permission
CREATE OR REPLACE FUNCTION user_has_permission(
    p_user_id UUID,
    p_resource VARCHAR,
    p_action VARCHAR,
    p_organization_id UUID
)
RETURNS BOOLEAN AS $$
DECLARE
    has_perm BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM user_roles ur
        INNER JOIN role_permissions rp ON ur.role_id = rp.role_id
        INNER JOIN permissions p ON rp.permission_id = p.id
        WHERE ur.user_id = p_user_id
          AND ur.organization_id = p_organization_id
          AND p.resource = p_resource
          AND p.action = p_action
    ) INTO has_perm;

    RETURN has_perm;
END;
$$ LANGUAGE plpgsql;

-- ============================================================================
-- AUTO-UPDATE TIMESTAMP TRIGGER
-- ============================================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_tenants_updated_at BEFORE UPDATE ON tenants
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_organizations_updated_at BEFORE UPDATE ON organizations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_users_updated_at BEFORE UPDATE ON users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- TEST DATA (for development only)
-- ============================================================================

-- Create test tenant: Bliek Vastgoed
INSERT INTO tenants (id, name, slug) VALUES
    ('11111111-1111-1111-1111-111111111111', 'Bliek Vastgoed', 'bliek-vastgoed');

-- Create test organization hierarchy
INSERT INTO organizations (id, tenant_id, parent_id, name, type) VALUES
    ('22222222-2222-2222-2222-222222222221', '11111111-1111-1111-1111-111111111111', NULL, 'Bliek Agency', 'agency'),
    ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222221', 'Building A - Main Street 123', 'building'),
    ('22222222-2222-2222-2222-222222222223', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222221', 'Building B - Oak Avenue 456', 'building'),
    ('22222222-2222-2222-2222-222222222224', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'Floor 1', 'floor'),
    ('22222222-2222-2222-2222-222222222225', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 'Floor 2', 'floor'),
    ('22222222-2222-2222-2222-222222222226', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222224', 'Room 101', 'room'),
    ('22222222-2222-2222-2222-222222222227', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222224', 'Room 102', 'room');

-- Create test users
-- Password: "Test123!" (BCrypt hash with cost 10)
INSERT INTO users (id, tenant_id, email, password_hash, first_name, last_name, email_verified) VALUES
    ('33333333-3333-3333-3333-333333333331', '11111111-1111-1111-1111-111111111111', 'admin@bliek.nl', '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36WQoeG6Lruj3vjPGga31lW', 'Admin', 'User', true),
    ('33333333-3333-3333-3333-333333333332', '11111111-1111-1111-1111-111111111111', 'manager@bliek.nl', '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36WQoeG6Lruj3vjPGga31lW', 'Manager', 'User', true),
    ('33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'agent@bliek.nl', '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36WQoeG6Lruj3vjPGga31lW', 'Agent', 'User', true),
    ('33333333-3333-3333-3333-333333333334', '11111111-1111-1111-1111-111111111111', 'viewer@bliek.nl', '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36WQoeG6Lruj3vjPGga31lW', 'Viewer', 'User', true);

-- Assign roles to users
INSERT INTO user_roles (user_id, role_id, organization_id) VALUES
    -- Admin user has Admin role in root organization
    ('33333333-3333-3333-3333-333333333331', '00000000-0000-0000-0000-000000000001', '22222222-2222-2222-2222-222222222221'),
    -- Manager user has Manager role in Building A
    ('33333333-3333-3333-3333-333333333332', '00000000-0000-0000-0000-000000000002', '22222222-2222-2222-2222-222222222222'),
    -- Agent user has Agent role in Floor 1
    ('33333333-3333-3333-3333-333333333333', '00000000-0000-0000-0000-000000000003', '22222222-2222-2222-2222-222222222224'),
    -- Viewer user has Viewer role in Room 101
    ('33333333-3333-3333-3333-333333333334', '00000000-0000-0000-0000-000000000004', '22222222-2222-2222-2222-222222222226');

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

-- List all users with their roles and organizations
-- SELECT
--     u.email,
--     u.first_name,
--     u.last_name,
--     r.name AS role,
--     o.name AS organization,
--     o.type AS org_type
-- FROM users u
-- INNER JOIN user_roles ur ON u.id = ur.user_id
-- INNER JOIN roles r ON ur.role_id = r.id
-- INNER JOIN organizations o ON ur.organization_id = o.id
-- WHERE u.deleted_at IS NULL
-- ORDER BY u.email;

-- Test permission check
-- SELECT user_has_permission(
--     '33333333-3333-3333-3333-333333333331', -- admin@bliek.nl
--     'users',
--     'create',
--     '22222222-2222-2222-2222-222222222221'  -- Bliek Agency
-- );

-- Get organization hierarchy
-- SELECT * FROM get_organization_ancestors('22222222-2222-2222-2222-222222222226'); -- Room 101
-- SELECT * FROM get_organization_descendants('22222222-2222-2222-2222-222222222222'); -- Building A
