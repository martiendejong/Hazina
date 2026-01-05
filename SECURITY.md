# Security Policy

## Overview

Hazina implements defense-in-depth security with encryption, input validation, secure headers, rate limiting, and automated vulnerability scanning.

## Reporting Security Issues

**Please DO NOT create public GitHub issues for security vulnerabilities.**

Instead, report security issues to: security@hazina.ai (or your organization's security email)

Include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

We aim to respond within 48 hours and provide a fix within 7 days for critical issues.

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |
| < 1.0   | :x:                |

## Security Features

### 1. Secret Management

**Encrypted Storage:**
- API keys encrypted at rest using ASP.NET Core Data Protection
- Keys stored in `%LocalAppData%\Hazina\DataProtection-Keys`
- Automatic key rotation every 90 days

**Usage:**
```csharp
services.AddHazinaSecurity();

// Store encrypted secret
await secretManager.StoreSecretAsync("openai-key", apiKey);

// Retrieve and decrypt
var key = await secretManager.GetSecretAsync("openai-key");
```

**Environment Variable Fallback:**
- Automatically falls back to environment variables
- Prevents accidental secret commits
- Supports Azure Key Vault, AWS Secrets Manager integration

### 2. Input Validation

**Protection Against:**
- SQL Injection
- Command Injection
- XSS (Cross-Site Scripting)
- Path Traversal
- LDAP Injection

**Validation Patterns:**
```csharp
services.AddHazinaInputValidation();

// Validate SQL input
var result = validator.ValidateInput(input, InputValidationType.Sql);
if (!result.IsValid)
{
    return BadRequest("Potential SQL injection detected");
}

// Sanitize HTML
var safe = validator.SanitizeInput(userInput, SanitizationType.Html);

// Validate file paths
if (!validator.IsSafeFilePath(path, basePath))
{
    return Forbid("Path traversal detected");
}
```

**Detection Patterns:**
- SQL: `UNION`, `DROP`, `--`, `;`, `xp_`, `sp_`
- Command: `|`, `&`, `;`, `$`, `` ` ``, `()`
- XSS: `<script>`, `javascript:`, `on*=`
- Path: `../`, `..\\`, `~`

### 3. Security Headers

**Implemented Headers:**
- `Strict-Transport-Security`: HSTS with 1-year max-age
- `Content-Security-Policy`: Restrict resource origins
- `X-Content-Type-Options`: Prevent MIME sniffing
- `X-Frame-Options`: Clickjacking protection
- `X-XSS-Protection`: Legacy XSS filter
- `Referrer-Policy`: Control referrer information
- `Permissions-Policy`: Feature permissions

**Configuration:**
```csharp
app.UseHazinaSecurityHeaders(options => {
    options.EnableStrictTransportSecurity = true;
    options.ContentSecurityPolicy = "default-src 'self'; script-src 'self' 'unsafe-inline'";
    options.XFrameOptionsValue = "DENY";
});
```

**Default CSP:**
```
default-src 'self';
script-src 'self' 'unsafe-inline' 'unsafe-eval';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https:;
font-src 'self' data:;
connect-src 'self';
frame-ancestors 'none';
```

### 4. Rate Limiting

**Protection Against:**
- Brute force attacks
- DDoS attacks
- API abuse
- Resource exhaustion

**Token Bucket Algorithm:**
- Configurable request limits
- Per-client tracking (IP-based)
- Automatic token refill
- Custom client identification

**Configuration:**
```csharp
app.UseHazinaRateLimiting(options => {
    options.MaxRequests = 100;        // 100 requests
    options.WindowSeconds = 60;        // per 60 seconds
    options.RetryAfterSeconds = 60;   // Retry-After header
    options.ExcludedPaths = new List<string> { "/health", "/metrics" };
});
```

**Response Headers:**
- `X-RateLimit-Limit`: Maximum requests allowed
- `X-RateLimit-Remaining`: Requests remaining
- `X-RateLimit-Reset`: Unix timestamp of window reset
- `Retry-After`: Seconds until retry (when limited)

### 5. Correlation IDs

**Distributed Tracing:**
- Unique request identifier
- Tracks requests across services
- Enables audit logging
- Facilitates incident investigation

**Configuration:**
```csharp
app.UseHazinaCorrelationId(options => {
    options.HeaderName = "X-Correlation-ID";
    options.IncludeInResponse = true;
    options.CorrelationIdGenerator = () => Guid.NewGuid().ToString("N");
});
```

**Usage:**
```csharp
var correlationId = HttpContext.GetCorrelationId();
logger.LogInformation("Processing request {CorrelationId}", correlationId);
```

### 6. Authentication & Authorization

**Recommended Patterns:**

**JWT Bearer Tokens:**
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "hazina",
            ValidAudience = "hazina-api",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });
```

**API Key Authentication:**
```csharp
services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options => {
        options.HeaderName = "X-API-Key";
        options.ValidateKey = async (key) => {
            return await apiKeyService.ValidateAsync(key);
        };
    });
```

### 7. Vulnerability Scanning

**Automated Scans:**
- **Trivy**: Filesystem and Docker image scanning
- **CodeQL**: Static application security testing (SAST)
- **Dependabot**: Dependency vulnerability alerts
- **SBOM**: Software Bill of Materials generation

**CI/CD Integration:**
```yaml
# .github/workflows/build-and-test.yml
- name: Run Trivy vulnerability scanner
  uses: aquasecurity/trivy-action@master
  with:
    scan-type: 'fs'
    severity: 'CRITICAL,HIGH'

- name: Initialize CodeQL
  uses: github/codeql-action/init@v3
  with:
    queries: +security-and-quality
```

**SARIF Upload:**
All security findings are uploaded to GitHub Security tab for tracking and remediation.

### 8. Container Security

**Docker Hardening:**
- Multi-stage builds (smaller attack surface)
- Non-root user execution
- Read-only root filesystem (where possible)
- Security updates in base image
- Minimal base image (aspnet:9.0)

**Dockerfile Best Practices:**
```dockerfile
# Use official Microsoft images
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install security updates
RUN apt-get update && apt-get upgrade -y

# Create non-root user
RUN groupadd -r hazina && useradd -r -g hazina hazina

# Switch to non-root user
USER hazina

# Health check
HEALTHCHECK --interval=30s --timeout=3s CMD curl -f http://localhost:8080/health
```

### 9. Data Protection

**Encryption at Rest:**
- API keys encrypted using Data Protection API
- Database connection strings in secure configuration
- Secrets in environment variables or key vaults

**Encryption in Transit:**
- TLS 1.2+ required for HTTPS
- Certificate validation enforced
- HSTS header enforces HTTPS

**PII Protection:**
- Structured logging avoids logging secrets
- Sensitive data masked in logs
- GDPR-compliant data handling patterns

### 10. Audit Logging

**Security Events Logged:**
- Authentication attempts (success/failure)
- Authorization failures
- Rate limit violations
- Input validation failures
- API key usage
- Suspicious patterns

**Log Format:**
```json
{
  "@timestamp": "2026-01-05T10:30:00.123Z",
  "level": "Warning",
  "message": "Rate limit exceeded",
  "correlationId": "abc123",
  "clientId": "192.168.1.100",
  "path": "/api/chat",
  "severity": "Medium"
}
```

## Security Best Practices

### For Developers

1. **Never commit secrets** to version control
2. **Use environment variables** for configuration
3. **Validate all user input** before processing
4. **Sanitize output** to prevent XSS
5. **Use parameterized queries** to prevent SQL injection
6. **Keep dependencies updated** (use Dependabot)
7. **Run security scans** before committing
8. **Follow principle of least privilege**
9. **Enable security logging** in production
10. **Review security alerts** regularly

### For Operators

1. **Rotate secrets** every 90 days minimum
2. **Use strong passwords** (16+ characters, mixed case, symbols)
3. **Enable TLS/HTTPS** for all public endpoints
4. **Configure firewall rules** to restrict access
5. **Monitor security logs** for suspicious activity
6. **Keep systems patched** and up to date
7. **Backup encryption keys** securely
8. **Test disaster recovery** procedures
9. **Implement network segmentation**
10. **Use intrusion detection systems**

## Compliance

### Standards Adherence

- **OWASP Top 10**: Protection against all major web vulnerabilities
- **CWE/SANS Top 25**: Mitigation of most dangerous software weaknesses
- **GDPR**: Data protection and privacy-by-design patterns
- **SOC 2**: Security, availability, confidentiality controls
- **NIST**: Cybersecurity framework alignment

### Security Controls

| Control | Implementation |
|---------|---------------|
| AC-1 | Access Control Policy (JWT, API Keys) |
| AU-2 | Audit Events (Structured Logging) |
| IA-2 | Identification & Authentication (Multi-provider) |
| SC-8 | Transmission Confidentiality (TLS 1.2+) |
| SC-13 | Cryptographic Protection (Data Protection API) |
| SC-28 | Protection at Rest (Encrypted secrets) |
| SI-3 | Malicious Code Protection (Trivy, CodeQL) |
| SI-10 | Input Validation (Comprehensive validators) |

## Security Tools

### Recommended Tools

**Static Analysis:**
- SonarQube
- Security Code Scan
- .NET Security Guard

**Dynamic Analysis:**
- OWASP ZAP
- Burp Suite
- Postman Security Tests

**Container Scanning:**
- Trivy
- Snyk Container
- Clair

**Dependency Scanning:**
- Dependabot
- Snyk Open Source
- WhiteSource

**Secret Scanning:**
- TruffleHog
- GitGuardian
- GitHub Secret Scanning

## Incident Response

### Incident Response Plan

1. **Detection**: Monitor logs, alerts, security events
2. **Containment**: Isolate affected systems
3. **Eradication**: Remove malicious code, patch vulnerabilities
4. **Recovery**: Restore systems from clean backups
5. **Lessons Learned**: Update security controls

### Contact Information

- **Security Team**: security@hazina.ai
- **Incident Response**: ir@hazina.ai
- **Emergency**: +1-555-SECURITY

### Response Times

| Severity | Response Time | Resolution Target |
|----------|---------------|-------------------|
| Critical | 1 hour | 24 hours |
| High | 4 hours | 72 hours |
| Medium | 1 business day | 7 days |
| Low | 3 business days | 30 days |

## Security Roadmap

### Planned Enhancements

- [ ] Mutual TLS (mTLS) for service-to-service authentication
- [ ] Hardware Security Module (HSM) integration
- [ ] Advanced threat detection with machine learning
- [ ] Automated penetration testing
- [ ] Security Information and Event Management (SIEM) integration
- [ ] Zero Trust Network Access (ZTNA)

## References

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)
- [Microsoft Security Development Lifecycle](https://www.microsoft.com/en-us/securityengineering/sdl)
- [GDPR Compliance](https://gdpr.eu/)

## Changelog

- **2026-01-05**: Initial security policy with Phase 1 security hardening
- **Future**: Planned enhancements for advanced threat detection

---

**Last Updated**: 2026-01-05
**Version**: 1.0.0
