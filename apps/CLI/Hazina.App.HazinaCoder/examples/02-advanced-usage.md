# Example 2: Advanced Usage

## Provider Selection

```bash
# Use specific provider
hazinacoder --provider anthropic "Write comprehensive tests"

# Auto-select best provider
hazinacoder --provider auto "Complex refactoring task"
```

## Streaming with Progress

```bash
# Watch tokens stream in real-time
hazinacoder --verbose "Generate API documentation"

# Output shows:
# ⏳ Analyzing codebase...
# 🔍 Found 15 API endpoints
# 📝 Generating docs... [###------] 40%
```

## Tool Chaining

```bash
# Natural language Git
hazinacoder "create a feature branch for dark mode,
then implement dark mode toggle,
then write tests,
then commit with descriptive message"
```

## Session Management

```bash
# Save your work
hazinacoder --save-session refactoring-2024

# Resume later
hazinacoder --load-session refactoring-2024
```
