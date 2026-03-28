-- Hazina.Identity - Database Test Queries
-- Run these to verify everything works

-- ============================================================================
-- 1. BASIC DATA CHECKS
-- ============================================================================

-- Should show 4 users
SELECT
    email,
    first_name || ' ' || last_name AS full_name,
    is_active,
    email_verified
FROM users
WHERE deleted_at IS NULL;

-- Should show 4 roles
SELECT name, description FROM roles ORDER BY name;

-- Should show 12 permissions
SELECT resource, action, description FROM permissions ORDER BY resource, action;

-- ============================================================================
-- 2. ORGANIZATION HIERARCHY
-- ============================================================================

-- Should show full tree: Agency → Buildings → Floors → Rooms
SELECT
    REPEAT('  ',
        CASE
            WHEN parent_id IS NULL THEN 0
            ELSE (SELECT COUNT(*) FROM get_organization_ancestors(o.id)) - 1
        END
    ) || name AS organization_tree,
    type
FROM organizations o
WHERE tenant_id = '11111111-1111-1111-1111-111111111111'
  AND deleted_at IS NULL
ORDER BY
    CASE WHEN parent_id IS NULL THEN id ELSE parent_id END,
    type DESC,
    name;

-- Test get_organization_descendants (should show Building A + 2 floors + 2 rooms = 5 rows)
SELECT * FROM get_organization_descendants('22222222-2222-2222-2222-222222222222');

-- Test get_organization_ancestors (Room 101 → Floor 1 → Building A → Agency = 4 rows)
SELECT * FROM get_organization_ancestors('22222222-2222-2222-2222-222222222226');

-- ============================================================================
-- 3. ROLE ASSIGNMENTS
-- ============================================================================

-- Should show which users have which roles in which organizations
SELECT
    u.email,
    r.name AS role,
    o.name AS organization,
    o.type AS org_type
FROM user_roles ur
JOIN users u ON ur.user_id = u.id
JOIN roles r ON ur.role_id = r.id
JOIN organizations o ON ur.organization_id = o.id
WHERE u.deleted_at IS NULL
ORDER BY u.email;

-- ============================================================================
-- 4. PERMISSION CHECKS
-- ============================================================================

-- Admin should have ALL permissions in Bliek Agency
SELECT
    p.resource,
    p.action,
    user_has_permission(
        '33333333-3333-3333-3333-333333333331', -- admin@bliek.nl
        p.resource,
        p.action,
        '22222222-2222-2222-2222-222222222221'  -- Bliek Agency
    ) AS admin_has_permission
FROM permissions p
ORDER BY p.resource, p.action;

-- Manager should have limited permissions (read/update orgs, read users)
SELECT
    p.resource,
    p.action,
    user_has_permission(
        '33333333-3333-3333-3333-333333333332', -- manager@bliek.nl
        p.resource,
        p.action,
        '22222222-2222-2222-2222-222222222222'  -- Building A
    ) AS manager_has_permission
FROM permissions p
ORDER BY p.resource, p.action;

-- Agent should only have read permissions
SELECT
    p.resource,
    p.action,
    user_has_permission(
        '33333333-3333-3333-3333-333333333333', -- agent@bliek.nl
        p.resource,
        p.action,
        '22222222-2222-2222-2222-222222222224'  -- Floor 1
    ) AS agent_has_permission
FROM permissions p
ORDER BY p.resource, p.action;

-- ============================================================================
-- 5. ROLE-PERMISSION MAPPINGS
-- ============================================================================

-- Show which permissions each role has
SELECT
    r.name AS role,
    COUNT(rp.permission_id) AS permission_count,
    STRING_AGG(p.resource || '.' || p.action, ', ' ORDER BY p.resource, p.action) AS permissions
FROM roles r
LEFT JOIN role_permissions rp ON r.id = rp.role_id
LEFT JOIN permissions p ON rp.permission_id = p.id
GROUP BY r.name
ORDER BY r.name;

-- ============================================================================
-- 6. AUDIT LOG (should be empty initially)
-- ============================================================================

SELECT
    a.action,
    u.email AS user,
    a.resource_type,
    a.created_at
FROM audit_logs a
LEFT JOIN users u ON a.user_id = u.id
ORDER BY a.created_at DESC
LIMIT 10;

-- ============================================================================
-- 7. DATA INTEGRITY CHECKS
-- ============================================================================

-- Check for orphaned user_roles (should return 0)
SELECT COUNT(*) AS orphaned_user_roles
FROM user_roles ur
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.id = ur.user_id AND u.deleted_at IS NULL);

-- Check for circular references in organizations (should return 0)
WITH RECURSIVE check_circular AS (
    SELECT id, parent_id, ARRAY[id] AS path
    FROM organizations
    WHERE deleted_at IS NULL

    UNION ALL

    SELECT o.id, o.parent_id, cc.path || o.id
    FROM organizations o
    JOIN check_circular cc ON o.id = cc.parent_id
    WHERE o.id = ANY(cc.path) AND o.deleted_at IS NULL
)
SELECT COUNT(*) AS circular_references FROM check_circular WHERE id = parent_id;

-- Check for users without roles (should return 0 if all test users have roles)
SELECT
    u.email,
    COUNT(ur.id) AS role_count
FROM users u
LEFT JOIN user_roles ur ON u.id = ur.user_id
WHERE u.deleted_at IS NULL
GROUP BY u.id, u.email
HAVING COUNT(ur.id) = 0;

-- ============================================================================
-- 8. PERFORMANCE CHECKS
-- ============================================================================

-- Explain plan for permission check (should use indexes)
EXPLAIN ANALYZE
SELECT user_has_permission(
    '33333333-3333-3333-3333-333333333331',
    'users',
    'create',
    '22222222-2222-2222-2222-222222222221'
);

-- Explain plan for organization hierarchy (should be efficient)
EXPLAIN ANALYZE
SELECT * FROM get_organization_descendants('22222222-2222-2222-2222-222222222221');

-- ============================================================================
-- 9. MANUAL TEST SCENARIOS
-- ============================================================================

-- Scenario 1: Create a new user
-- INSERT INTO users (tenant_id, email, password_hash, first_name, last_name, email_verified)
-- VALUES (
--     '11111111-1111-1111-1111-111111111111',
--     'newuser@bliek.nl',
--     '$2a$10$EixZaYVK1fsbw1ZfbX3OXePaWxn96p36WQoeG6Lruj3vjPGga31lW',
--     'New',
--     'User',
--     true
-- );

-- Scenario 2: Assign Agent role to new user in Building B
-- INSERT INTO user_roles (user_id, role_id, organization_id)
-- VALUES (
--     (SELECT id FROM users WHERE email = 'newuser@bliek.nl'),
--     '00000000-0000-0000-0000-000000000003', -- Agent role
--     '22222222-2222-2222-2222-222222222223'  -- Building B
-- );

-- Scenario 3: Create a new building
-- INSERT INTO organizations (tenant_id, parent_id, name, type)
-- VALUES (
--     '11111111-1111-1111-1111-111111111111',
--     '22222222-2222-2222-2222-222222222221', -- Bliek Agency
--     'Building C - Park Lane 789',
--     'building'
-- );

-- ============================================================================
-- SUCCESS CRITERIA
-- ============================================================================

-- If all queries above return expected results:
-- ✅ Database schema is correct
-- ✅ Test data is loaded
-- ✅ Indexes are working
-- ✅ Helper functions are working
-- ✅ No data integrity issues
-- ✅ Ready for C# model implementation
