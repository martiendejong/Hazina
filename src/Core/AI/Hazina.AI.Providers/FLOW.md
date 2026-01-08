# Provider Orchestrator Flow

## Happy Path: Simple Request

```
1. User calls orchestrator.GetResponse(messages)
2. ProviderSelector picks provider based on strategy
3. Request sent to selected provider
4. Response received
5. CostTracker records usage
6. Response returned to caller
```

## Happy Path: With Failover

```
1. User calls orchestrator.GetResponse(messages)
2. ProviderSelector picks primary provider (Priority strategy)
3. Request sent to primary provider
4. Primary provider fails (timeout/error)
5. CircuitBreaker trips if threshold exceeded
6. FailoverHandler selects next provider
7. Request sent to fallback provider
8. Response received
9. CostTracker records usage
10. Response returned to caller
```

## Sequence Diagram

```
User          Orchestrator      Selector        Provider        CostTracker
  │                │               │               │                │
  │  GetResponse() │               │               │                │
  │───────────────►│               │               │                │
  │                │  Select()     │               │                │
  │                │──────────────►│               │                │
  │                │◄──────────────│               │                │
  │                │   "openai"    │               │                │
  │                │               │               │                │
  │                │  GetResponse()│               │                │
  │                │──────────────────────────────►│                │
  │                │◄──────────────────────────────│                │
  │                │   Response    │               │                │
  │                │               │               │                │
  │                │  RecordUsage()│               │                │
  │                │───────────────────────────────────────────────►│
  │                │               │               │                │
  │◄───────────────│               │               │                │
  │   Response     │               │               │                │
```

## Error Paths

### Provider Timeout
```
1. Request sent to provider
2. No response within timeout (default: 30s)
3. CircuitBreaker records failure
4. If failures >= threshold: circuit opens
5. FailoverHandler tries next provider
6. If all providers fail: throw AggregateException
```

### Rate Limited
```
1. Provider returns 429 Too Many Requests
2. RetryPolicy activates
3. Wait with exponential backoff (1s, 2s, 4s...)
4. Retry request
5. If max retries exceeded: failover to next provider
```

### Budget Exceeded
```
1. Request would exceed budget
2. BudgetManager rejects request
3. Provider skipped in selection
4. Next provider selected (if available)
5. If no providers within budget: throw BudgetExceededException
```

### All Providers Unhealthy
```
1. HealthMonitor marks all providers unhealthy
2. ProviderSelector finds no healthy providers
3. Wait for circuit breaker reset (default: 60s)
4. Or throw NoHealthyProvidersException
```

## Selection Strategy Details

| Strategy | How It Works | When to Use |
|----------|--------------|-------------|
| Priority | Pick lowest priority number that's healthy | Default, most common |
| LeastCost | Pick cheapest provider per token | Cost optimization |
| FastestResponse | Pick provider with lowest avg latency | Low latency requirements |
| RoundRobin | Rotate through providers | Load distribution |
| Random | Random healthy provider | Testing, fairness |
| Specific | Use named provider only | Testing specific provider |

## Key Decision Points

```
                    Request arrives
                         │
                         ▼
              ┌─────────────────────┐
              │ Check budget limit  │
              └─────────────────────┘
                    │         │
                 Within     Exceeded
                    │         │
                    ▼         ▼
              ┌─────────┐  Skip provider
              │ Select  │
              │ provider│
              └─────────┘
                    │
                    ▼
              ┌─────────────────────┐
              │ Check health status │
              └─────────────────────┘
                    │         │
                 Healthy   Unhealthy
                    │         │
                    ▼         ▼
              Send request   Failover
                    │
                    ▼
              ┌─────────────────────┐
              │   Success?          │
              └─────────────────────┘
                    │         │
                  Yes        No
                    │         │
                    ▼         ▼
              Return      Circuit breaker
              response    + Failover
```
