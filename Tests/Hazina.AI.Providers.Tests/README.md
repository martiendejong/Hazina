# Hazina.AI.Providers Tests

## Status

**CostTrackerTests**: ✅ Fully functional (13 tests)
**ProviderOrchestratorTests**: ⏳ Limited (functionality tests only - integration tests pending)
**ProviderSelectorTests**: ⏳ Limited (functionality tests only - requires internal API access)

## Coverage Summary

| Component | Tests | Coverage | Status |
|-----------|-------|----------|--------|
| CostTracker | 13 | ~90% | ✅ Complete |
| ProviderOrchestrator | 5 | ~40% | ⏳ Basic |
| ProviderSelector | 0 | 0% | ❌ Blocked by API access |

## Known Limitations

### ProviderRegistry
- `RegisterProvider()` is internal/private - cannot be tested directly
- Tests must go through `ProviderOrchestrator.RegisterProvider()` instead

### ProviderHealthStatus
- `IsHealthy` property is read-only (calculated property)
- Cannot be set directly in test mocks
- Tests must rely on `State` property only

### ProviderOrchestrator
- No `DisableProvider()` / `EnableProvider()` public methods found
- Integration tests with real providers require API keys
- Mock-based unit tests are limited

## Next Steps

1. **Add Integration Tests** - Test with real providers (requires API keys from environment)
2. **Test Internal API** - Use InternalsVisibleTo attribute to expose internals to test project
3. **Add More Coverage** - Target 70%+ code coverage with additional test scenarios
4. **Performance Tests** - Benchmark provider selection strategies

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~CostTrackerTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Organization

```
tests/
└── Hazina.AI.Providers.Tests/
    ├── CostTrackerTests.cs          ✅ 13 tests, fully functional
    ├── ProviderOrchestratorTests.cs ⏳ 5 tests, basic coverage
    └── README.md                    📖 This file
```
