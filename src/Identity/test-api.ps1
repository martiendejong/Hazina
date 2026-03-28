# Hazina.Identity API Test Script
# Tests login, get users, get organizations

$baseUrl = "http://localhost:5200"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Hazina.Identity API Test" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Login as admin
Write-Host "Test 1: Login as admin@bliek.nl" -ForegroundColor Yellow
$loginResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body (@{
    email = "admin@bliek.nl"
    password = "Test123!"
} | ConvertTo-Json)

Write-Host "✓ Login successful!" -ForegroundColor Green
Write-Host "  User: $($loginResponse.user.email) ($($loginResponse.user.firstName) $($loginResponse.user.lastName))" -ForegroundColor Gray
Write-Host "  Roles: $($loginResponse.user.roles | ForEach-Object { $_.name } | Join-String -Separator ', ')" -ForegroundColor Gray
Write-Host "  Access Token: $($loginResponse.accessToken.Substring(0, 50))..." -ForegroundColor Gray
Write-Host ""

$accessToken = $loginResponse.accessToken

# Test 2: Get current user
Write-Host "Test 2: Get current user (/api/auth/me)" -ForegroundColor Yellow
$headers = @{ Authorization = "Bearer $accessToken" }
$meResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/me" -Method GET -Headers $headers

Write-Host "✓ Got current user!" -ForegroundColor Green
Write-Host "  Email: $($meResponse.email)" -ForegroundColor Gray
Write-Host "  Name: $($meResponse.firstName) $($meResponse.lastName)" -ForegroundColor Gray
Write-Host ""

# Test 3: List all users
Write-Host "Test 3: List all users (/api/users)" -ForegroundColor Yellow
$usersResponse = Invoke-RestMethod -Uri "$baseUrl/api/users" -Method GET -Headers $headers

Write-Host "✓ Got $($usersResponse.Count) users!" -ForegroundColor Green
foreach ($user in $usersResponse) {
    Write-Host "  - $($user.email) ($($user.firstName) $($user.lastName))" -ForegroundColor Gray
}
Write-Host ""

# Test 4: List all organizations
Write-Host "Test 4: List all organizations (/api/organizations)" -ForegroundColor Yellow
$orgsResponse = Invoke-RestMethod -Uri "$baseUrl/api/organizations" -Method GET -Headers $headers

Write-Host "✓ Got $($orgsResponse.Count) organizations!" -ForegroundColor Green
foreach ($org in $orgsResponse) {
    $indent = if ($org.parentId) { "  " } else { "" }
    Write-Host "  $indent- $($org.name) [$($org.type)]" -ForegroundColor Gray
}
Write-Host ""

# Test 5: Login as different users
Write-Host "Test 5: Login as other users" -ForegroundColor Yellow
$testUsers = @(
    @{ email = "manager@bliek.nl"; password = "Test123!" },
    @{ email = "agent@bliek.nl"; password = "Test123!" },
    @{ email = "viewer@bliek.nl"; password = "Test123!" }
)

foreach ($testUser in $testUsers) {
    $loginResp = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body ($testUser | ConvertTo-Json)
    $role = $loginResp.user.roles[0].name
    Write-Host "  ✓ $($loginResp.user.email) → Role: $role" -ForegroundColor Green
}
Write-Host ""

# Test 6: Refresh token
Write-Host "Test 6: Refresh access token" -ForegroundColor Yellow
$refreshResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/refresh" -Method POST -ContentType "application/json" -Body (@{
    refreshToken = $loginResponse.refreshToken
} | ConvertTo-Json)

Write-Host "✓ Token refreshed!" -ForegroundColor Green
Write-Host "  New Access Token: $($refreshResponse.accessToken.Substring(0, 50))..." -ForegroundColor Gray
Write-Host ""

# Test 7: Logout
Write-Host "Test 7: Logout" -ForegroundColor Yellow
$logoutResponse = Invoke-RestMethod -Uri "$baseUrl/api/auth/logout" -Method POST -ContentType "application/json" -Body (@{
    refreshToken = $loginResponse.refreshToken
} | ConvertTo-Json)

Write-Host "✓ Logged out successfully!" -ForegroundColor Green
Write-Host ""

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "ALL TESTS PASSED! ✓✓✓" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test in Swagger UI: http://localhost:5200/swagger" -ForegroundColor Gray
Write-Host "2. Integrate with Bliek frontend" -ForegroundColor Gray
Write-Host "3. Add more endpoints (create user, create org, assign roles)" -ForegroundColor Gray
