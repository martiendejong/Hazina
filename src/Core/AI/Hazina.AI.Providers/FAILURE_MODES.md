# Failure Modes - Provider Orchestrator

## Quick Reference

| Failure | Severity | Auto-Recovery | Manual Action |
|---------|----------|---------------|---------------|
| Single provider timeout | DEGRADED | Yes (failover) | None |
| All providers timeout | CRITICAL | No | Check APIs |
| Rate limited | DEGRADED | Yes (backoff) | None |
| Invalid API key | CRITICAL | No | Fix config |
| Budget exceeded | DEGRADED | No | Reset budget |
| Circuit breaker open | DEGRADED | Yes (60s) | Reset manually |
| Network failure | CRITICAL | No | Check network |

---

## Provider Layer Failures

### Single Provider Timeout
```
Severity: DEGRADED
Symptoms: Request takes > 30s, no response
Impact: Increased latency, but request completes

Recovery:
1. [AUTO] CircuitBreaker records failure
2. [AUTO] FailoverHandler tries next provider
3. [AUTO] Request completes via fallback

Prevention:
- Register multiple providers
- Set appropriate timeouts
- Enable health monitoring
```

### All Providers Down
```
Severity: CRITICAL
Symptoms: AggregateException with all provider failures
Impact: Complete service outage for LLM features

Recovery:
1. [MANUAL] Check provider status pages:
   - status.openai.com
   - status.anthropic.com
2. [MANUAL] Verify API keys valid
3. [MANUAL] Check network connectivity
4. [AUTO] Circuit breakers reset after 60s

Prevention:
- Use providers from different vendors
- Implement degraded mode (cached responses)
- Alert on all-provider failure
```

### Rate Limited (429)
```
Severity: DEGRADED
Symptoms: 429 Too Many Requests
Impact: Temporary slowdown

Recovery:
1. [AUTO] RetryPolicy waits with backoff
2. [AUTO] Retry after 1s, 2s, 4s...
3. [AUTO] Failover if max retries exceeded

Prevention:
- Implement request queuing
- Use multiple API keys
- Monitor rate limit headers
- Spread load across providers
```

### Invalid API Key
```
Severity: CRITICAL
Symptoms: 401 Unauthorized, immediate failure
Impact: Provider completely unavailable

Recovery:
1. [MANUAL] Verify API key in config
2. [MANUAL] Check key hasn't expired
3. [MANUAL] Regenerate key if compromised
4. [MANUAL] Update config and restart

Prevention:
- Use environment variables for keys
- Rotate keys periodically
- Alert on 401 errors
```

### Budget Exceeded
```
Severity: DEGRADED
Symptoms: BudgetExceededException
Impact: Provider unavailable until reset

Recovery:
1. [AUTO] Provider skipped in selection
2. [AUTO] Fallback to other providers
3. [MANUAL] Increase budget or reset

Prevention:
- Set realistic budgets
- Configure alerts at 50%, 75%, 90%
- Use cost-optimized provider selection
```

---

## Infrastructure Failures

### Circuit Breaker Open
```
Severity: DEGRADED
Symptoms: Provider marked unhealthy, skipped
Impact: Reduced provider pool

Recovery:
1. [AUTO] Circuit resets after timeout (60s default)
2. [AUTO] Half-open state tests one request
3. [AUTO] Closes if test succeeds
4. [MANUAL] Reset via ResetCircuitBreaker()

Prevention:
- Tune failure thresholds appropriately
- Don't set thresholds too sensitive
- Monitor circuit breaker state
```

### Health Check Failure
```
Severity: DEGRADED
Symptoms: Provider health = Unhealthy
Impact: Provider excluded from selection

Recovery:
1. [AUTO] HealthMonitor retries periodically
2. [AUTO] Provider restored when healthy
3. [MANUAL] Check provider-specific issues

Prevention:
- Configure health check interval
- Use lightweight health check requests
- Monitor health status metrics
```

### Network Connectivity
```
Severity: CRITICAL
Symptoms: SocketException, timeout on all providers
Impact: Complete LLM service outage

Recovery:
1. [MANUAL] Check network connectivity
2. [MANUAL] Verify DNS resolution
3. [MANUAL] Check firewall rules
4. [MANUAL] Verify proxy configuration

Prevention:
- Monitor network health
- Use redundant network paths
- Implement offline fallback mode
```

---

## Configuration Failures

### No Providers Registered
```
Severity: CRITICAL
Symptoms: InvalidOperationException on first call
Impact: Cannot make any LLM calls

Recovery:
1. [MANUAL] Register at least one provider
2. [MANUAL] Use QuickSetup helpers

Prevention:
- Validate configuration at startup
- Fail fast if no providers
```

### Invalid Selection Strategy
```
Severity: CRITICAL
Symptoms: ArgumentException
Impact: Cannot select providers

Recovery:
1. [MANUAL] Use valid SelectionStrategy enum
2. [MANUAL] Check strategy configuration

Prevention:
- Use strongly-typed configuration
- Validate at startup
```

---

## Monitoring Checklist

### Metrics to Watch
- [ ] Request success rate (target: > 99%)
- [ ] Average latency (target: < 2s)
- [ ] Cost per request (target: within budget)
- [ ] Provider health status (target: >= 1 healthy)
- [ ] Circuit breaker state (target: mostly closed)
- [ ] Failover rate (target: < 5%)

### Alerts to Configure
- [ ] All providers unhealthy
- [ ] Budget > 90% consumed
- [ ] Error rate > 5%
- [ ] Latency > 10s
- [ ] Circuit breaker opens frequently

---

## Recovery Procedures

### Emergency: All Providers Down
```bash
1. Check status pages
2. Verify network: ping api.openai.com
3. Verify keys: test with curl
4. Reset circuit breakers programmatically:
   orchestrator.ResetCircuitBreaker("openai");
   orchestrator.ResetCircuitBreaker("anthropic");
5. Restart application if needed
```

### Emergency: Cost Spike
```bash
1. Check cost tracker: orchestrator.GetCostByProvider()
2. Identify high-cost provider
3. Disable temporarily: orchestrator.SetProviderEnabled("openai", false)
4. Switch to cost-optimized strategy
5. Investigate cause (loops? large prompts?)
```
