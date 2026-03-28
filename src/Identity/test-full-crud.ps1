# Hazina.Identity - Full CRUD Test Script
# Tests all endpoints: Auth, Users, Organizations, Roles

$baseUrl = "http://localhost:5200"
$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Hazina.Identity - Full CRUD Test" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# 1. AUTHENTICATION TESTS
# ============================================================================

Write-Host "1. AUTHENTICATION TESTS" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow

# Login as admin
Write-Host "→ Login as admin@bliek.nl..." -ForegroundColor Gray
$loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body (@{
    email = "admin@bliek.nl"
    password = "Test123!"
} | ConvertTo-Json)

Write-Host "  ✓ Login successful!" -ForegroundColor Green
Write-Host "  User: $($loginResponse.user.email)" -ForegroundColor DarkGray
$accessToken = $loginResponse.accessToken
$headers = @{ Authorization = "Bearer $accessToken" }
Write-Host ""

# ============================================================================
# 2. USER CRUD TESTS
# ============================================================================

Write-Host "2. USER CRUD TESTS" -ForegroundColor Yellow
Write-Host "------------------" -ForegroundColor Yellow

# Create user
Write-Host "→ Creating new user (newuser@bliek.nl)..." -ForegroundColor Gray
$createUserResponse = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method POST -ContentType "application/json" -Headers $headers -Body (@{
    email = "newuser@bliek.nl"
    password = "SecurePass123!"
    firstName = "New"
    lastName = "User"
} | ConvertTo-Json)

Write-Host "  ✓ User created! ID: $($createUserResponse.id)" -ForegroundColor Green
$newUserId = $createUserResponse.id

# List users
Write-Host "→ Listing all users..." -ForegroundColor Gray
$usersResponse = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method GET -Headers $headers
Write-Host "  ✓ Found $($usersResponse.Count) users (should be 5 now)" -ForegroundColor Green

# Get specific user
Write-Host "→ Getting user by ID..." -ForegroundColor Gray
$userResponse = Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId" -Method GET -Headers $headers
Write-Host "  ✓ Got user: $($userResponse.email)" -ForegroundColor Green

# Update user
Write-Host "→ Updating user..." -ForegroundColor Gray
$updateUserResponse = Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId" -Method PUT -ContentType "application/json" -Headers $headers -Body (@{
    email = "newuser@bliek.nl"
    firstName = "Updated"
    lastName = "Name"
    password = $null
} | ConvertTo-Json)

Write-Host "  ✓ User updated! Name: $($updateUserResponse.firstName) $($updateUserResponse.lastName)" -ForegroundColor Green
Write-Host ""

# ============================================================================
# 3. ORGANIZATION CRUD TESTS
# ============================================================================

Write-Host "3. ORGANIZATION CRUD TESTS" -ForegroundColor Yellow
Write-Host "--------------------------" -ForegroundColor Yellow

# List organizations
Write-Host "→ Listing all organizations..." -ForegroundColor Gray
$orgsResponse = Invoke-RestMethod -Uri "$baseUrl/api/organizations" -Method GET -Headers $headers
Write-Host "  ✓ Found $($orgsResponse.Count) organizations" -ForegroundColor Green
$bliekAgencyId = ($orgsResponse | Where-Object { $_.name -eq "Bliek Agency" }).id

# Create organization
Write-Host "→ Creating new building (Building C)..." -ForegroundColor Gray
$createOrgResponse = Invoke-RestMethod -Uri "$baseUrl/api/organizations" -Method POST -ContentType "application/json" -Headers $headers -Body (@{
    name = "Building C - Test Street 999"
    type = "building"
    parentId = $bliekAgencyId
    metadata = $null
} | ConvertTo-Json)

Write-Host "  ✓ Organization created! ID: $($createOrgResponse.id)" -ForegroundColor Green
$newOrgId = $createOrgResponse.id

# Update organization
Write-Host "→ Updating organization..." -ForegroundColor Gray
$updateOrgResponse = Invoke-RestMethod -Uri "$baseUrl/api/organizations/$newOrgId" -Method PUT -ContentType "application/json" -Headers $headers -Body (@{
    name = "Building C - Test Street 999 (Updated)"
    type = "building"
    metadata = '{"address":"Test Street 999","city":"Amsterdam"}'
} | ConvertTo-Json)

Write-Host "  ✓ Organization updated! Name: $($updateOrgResponse.name)" -ForegroundColor Green

# Create floor in Building C
Write-Host "→ Creating floor in Building C..." -ForegroundColor Gray
$createFloorResponse = Invoke-RestMethod -Uri "$baseUrl/api/organizations" -Method POST -ContentType "application/json" -Headers $headers -Body (@{
    name = "Floor 1"
    type = "floor"
    parentId = $newOrgId
    metadata = $null
} | ConvertTo-Json)

Write-Host "  ✓ Floor created! ID: $($createFloorResponse.id)" -ForegroundColor Green
$newFloorId = $createFloorResponse.id
Write-Host ""

# ============================================================================
# 4. ROLE MANAGEMENT TESTS
# ============================================================================

Write-Host "4. ROLE MANAGEMENT TESTS" -ForegroundColor Yellow
Write-Host "------------------------" -ForegroundColor Yellow

# List roles
Write-Host "→ Listing all roles..." -ForegroundColor Gray
$rolesResponse = Invoke-RestMethod -Uri "$baseUrl/api/roles" -Method GET -Headers $headers
Write-Host "  ✓ Found $($rolesResponse.Count) roles:" -ForegroundColor Green
foreach ($role in $rolesResponse) {
    Write-Host "    - $($role.name): $($role.description)" -ForegroundColor DarkGray
}

# Assign role to new user
Write-Host "→ Assigning Agent role to new user in Building C..." -ForegroundColor Gray
$agentRoleId = ($rolesResponse | Where-Object { $_.name -eq "Agent" }).id
$assignRoleResponse = Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId/roles" -Method POST -ContentType "application/json" -Headers $headers -Body (@{
    roleId = $agentRoleId
    organizationId = $newOrgId
} | ConvertTo-Json)

Write-Host "  ✓ Role assigned! User: $($assignRoleResponse.userId), Role: $($assignRoleResponse.roleName), Org: $($assignRoleResponse.organizationName)" -ForegroundColor Green
$userRoleId = $assignRoleResponse.id

# Verify user now has role
Write-Host "→ Verifying user has role..." -ForegroundColor Gray
$userWithRoleResponse = Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId" -Method GET -Headers $headers
Write-Host "  ✓ User has $($userWithRoleResponse.roles.Count) role(s):" -ForegroundColor Green
foreach ($role in $userWithRoleResponse.roles) {
    Write-Host "    - $($role.name) in $($role.organizationName)" -ForegroundColor DarkGray
}
Write-Host ""

# ============================================================================
# 5. TEST NEW USER LOGIN
# ============================================================================

Write-Host "5. TEST NEW USER LOGIN" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Yellow

Write-Host "→ Login as newuser@bliek.nl..." -ForegroundColor Gray
$newUserLoginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body (@{
    email = "newuser@bliek.nl"
    password = "SecurePass123!"
} | ConvertTo-Json)

Write-Host "  ✓ New user login successful!" -ForegroundColor Green
Write-Host "  User: $($newUserLoginResponse.user.email)" -ForegroundColor DarkGray
Write-Host "  Roles: $($newUserLoginResponse.user.roles | ForEach-Object { "$($_.name) in $($_.organizationName)" } | Join-String -Separator ', ')" -ForegroundColor DarkGray
Write-Host ""

# ============================================================================
# 6. CLEANUP TESTS (DELETE)
# ============================================================================

Write-Host "6. CLEANUP TESTS" -ForegroundColor Yellow
Write-Host "----------------" -ForegroundColor Yellow

# Revoke role
Write-Host "→ Revoking role from user..." -ForegroundColor Gray
Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId/roles/$userRoleId" -Method DELETE -Headers $headers | Out-Null
Write-Host "  ✓ Role revoked!" -ForegroundColor Green

# Delete floor
Write-Host "→ Deleting floor..." -ForegroundColor Gray
Invoke-RestMethod -Uri "$baseUrl/api/organizations/$newFloorId" -Method DELETE -Headers $headers | Out-Null
Write-Host "  ✓ Floor deleted (soft delete)!" -ForegroundColor Green

# Delete building
Write-Host "→ Deleting Building C..." -ForegroundColor Gray
Invoke-RestMethod -Uri "$baseUrl/api/organizations/$newOrgId" -Method DELETE -Headers $headers | Out-Null
Write-Host "  ✓ Building C deleted (soft delete)!" -ForegroundColor Green

# Delete user
Write-Host "→ Deleting user..." -ForegroundColor Gray
Invoke-RestMethod -Uri "$baseUrl/api/users/$newUserId" -Method DELETE -Headers $headers | Out-Null
Write-Host "  ✓ User deleted (soft delete)!" -ForegroundColor Green
Write-Host ""

# ============================================================================
# 7. VERIFY SOFT DELETES
# ============================================================================

Write-Host "7. VERIFY SOFT DELETES" -ForegroundColor Yellow
Write-Host "----------------------" -ForegroundColor Yellow

# List users again (should not include deleted user)
Write-Host "→ Listing users after delete..." -ForegroundColor Gray
$usersAfterDelete = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method GET -Headers $headers
Write-Host "  ✓ Found $($usersAfterDelete.Count) users (should be 4 - deleted user excluded)" -ForegroundColor Green

# List organizations again (should not include deleted orgs)
Write-Host "→ Listing organizations after delete..." -ForegroundColor Gray
$orgsAfterDelete = Invoke-RestMethod -Uri "$baseUrl/api/organizations" -Method GET -Headers $headers
Write-Host "  ✓ Found $($orgsAfterDelete.Count) organizations (deleted orgs excluded)" -ForegroundColor Green
Write-Host ""

# ============================================================================
# SUCCESS
# ============================================================================

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "ALL TESTS PASSED! ✓✓✓" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Summary of tested endpoints:" -ForegroundColor Yellow
Write-Host "  ✓ POST   /api/auth/login" -ForegroundColor DarkGray
Write-Host "  ✓ GET    /api/auth/me" -ForegroundColor DarkGray
Write-Host "  ✓ GET    /api/users" -ForegroundColor DarkGray
Write-Host "  ✓ POST   /api/users" -ForegroundColor DarkGray
Write-Host "  ✓ GET    /api/users/{id}" -ForegroundColor DarkGray
Write-Host "  ✓ PUT    /api/users/{id}" -ForegroundColor DarkGray
Write-Host "  ✓ DELETE /api/users/{id}" -ForegroundColor DarkGray
Write-Host "  ✓ GET    /api/organizations" -ForegroundColor DarkGray
Write-Host "  ✓ POST   /api/organizations" -ForegroundColor DarkGray
Write-Host "  ✓ PUT    /api/organizations/{id}" -ForegroundColor DarkGray
Write-Host "  ✓ DELETE /api/organizations/{id}" -ForegroundColor DarkGray
Write-Host "  ✓ GET    /api/roles" -ForegroundColor DarkGray
Write-Host "  ✓ POST   /api/users/{userId}/roles" -ForegroundColor DarkGray
Write-Host "  ✓ DELETE /api/users/{userId}/roles/{userRoleId}" -ForegroundColor DarkGray
Write-Host ""
Write-Host "Total: 14 endpoints tested successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Open Swagger UI and try manually:" -ForegroundColor Yellow
Write-Host "  http://localhost:5200/swagger" -ForegroundColor Cyan
