# Troubleshooting Guide
**Iteration 19: User support**

## Common Issues

### 1. "API key not found"

**Problem:** Provider API key not configured

**Solution:**
```bash
# Set environment variable
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."

# Or add to appsettings.json
{
  "Providers": {
    "OpenAI": {
      "ApiKey": "sk-..."
    }
  }
}
```

### 2. "Provider timeout"

**Problem:** Network issues or provider downtime

**Solution:**
```bash
# Use different provider
hazinacoder --provider anthropic "your prompt"

# Check health
hazinacoder --health
```

### 3. "Out of memory"

**Problem:** Large context or too many concurrent operations

**Solution:**
```bash
# Reduce max turns
hazinacoder --max-turns 20 "your prompt"

# Clear cache
rm -rf .hazinacoder/cache/
```

### 4. "Tests failing"

**Problem:** Missing dependencies or outdated packages

**Solution:**
```bash
dotnet restore
dotnet build
dotnet test
```

### 5. "Streaming not working"

**Problem:** Feature flag disabled or network issues

**Solution:**
```json
// appsettings.json
{
  "FeatureFlags": {
    "TokenStreaming": true
  }
}
```

## Debug Mode

Enable verbose output:
```bash
hazinacoder --verbose "your prompt"
```

## Getting Help

1. Check logs: `.hazinacoder/logs/`
2. Run health check: `hazinacoder --health`
3. Review config: `appsettings.json`
4. [GitHub Issues](https://github.com/hazina-ai/hazina/issues)

## Performance Issues

### Slow Response Time

```bash
# Check provider performance
hazinacoder --benchmark

# Use faster model
hazinacoder --model gpt-3.5-turbo "prompt"
```

### High Costs

```bash
# Check costs
hazinacoder --cost-report

# Set budget
# In appsettings.json:
{
  "Monitoring": {
    "CostBudgetDaily": 5.00
  }
}
```

## Still Stuck?

Open an issue with:
- Error message
- Log file (`.hazinacoder/logs/latest.log`)
- Configuration (redact API keys!)
- Steps to reproduce
