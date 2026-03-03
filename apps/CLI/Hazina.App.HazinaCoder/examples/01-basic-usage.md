# Example 1: Basic Usage

## Simple Query

```bash
hazinacoder "Explain what this function does: factorial(n)"
```

## With File Context

```bash
# HazinaCoder automatically reads nearby files
cd my-project/src
hazinacoder "Add error handling to UserService.cs"
```

## Output:

```
🤖 HazinaCoder analyzing UserService.cs...

I'll add comprehensive error handling:

1. Null argument validation
2. Try-catch blocks for database operations
3. Logging of exceptions
4. User-friendly error messages

[Shows code diff]

Would you like me to apply these changes? [Y/n]
```

## Next Steps

- [Advanced Usage](02-advanced-usage.md)
- [Vision Analysis](03-vision.md)
- [Multi-Agent](04-multi-agent.md)
