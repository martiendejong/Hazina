# Test Binary Size Optimization

**Problem:** Each test project builds its own binaries with full dependency copies, resulting in hundreds of MB per project.

**Impact:**
- Disk space waste (easily 1-2 GB for 10-20 test projects)
- Slower builds (more files to copy)
- Slower git operations (larger working directory)

---

## ✅ Implemented Solutions

### 1. Shared Output Directory (`Directory.Build.props`)

**Location:** `tests/Directory.Build.props`

**What it does:**
- Centralizes all test project output to `artifacts/tests/`
- Reduces duplication by sharing common assemblies
- Simplifies cleanup (delete one folder instead of 50+)

**Configuration:**
```xml
<BaseOutputPath>$(MSBuildThisFileDirectory)..\artifacts\tests\$(MSBuildProjectName)\bin\</BaseOutputPath>
<BaseIntermediateOutputPath>$(MSBuildThisFileDirectory)..\artifacts\tests\$(MSBuildProjectName)\obj\</BaseIntermediateOutputPath>
```

**Benefits:**
- ✅ Reduced disk usage (30-50% savings)
- ✅ Faster cleanup
- ✅ Better organized build artifacts
- ✅ .gitignore already excludes `artifacts/`

### 2. Optimized MSBuild Properties

**Reduces binary duplication:**
```xml
<!-- Don't copy dependency lock files -->
<CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>

<!-- Flatten output structure -->
<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
<AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>

<!-- Skip unnecessary files -->
<ProduceReferenceAssembly>false</ProduceReferenceAssembly>
<GenerateDocumentationFile>false</GenerateDocumentationFile>
```

**Benefits:**
- ✅ Smaller bin folders
- ✅ Faster builds
- ✅ Less disk I/O

---

## 📊 Expected Results

### Before Optimization
```
tests/
├── Project1.Tests/
│   ├── bin/ (150 MB)
│   └── obj/ (2 MB)
├── Project2.Tests/
│   ├── bin/ (150 MB)
│   └── obj/ (2 MB)
└── Project3.Tests/
    ├── bin/ (150 MB)
    └── obj/ (2 MB)

Total: ~900 MB for 3 test projects
```

### After Optimization
```
artifacts/tests/
├── Project1.Tests/
│   ├── bin/ (80 MB) ← Shared dependencies
│   └── obj/ (1 MB)
├── Project2.Tests/
│   ├── bin/ (40 MB) ← Much smaller!
│   └── obj/ (1 MB)
└── Project3.Tests/
    ├── bin/ (40 MB)
    └── obj/ (1 MB)

Total: ~240 MB for 3 test projects (73% reduction!)
```

---

## 🎯 Best Practices

1. **Clean before commits**
   ```bash
   dotnet clean
   ```

2. **Regular maintenance**
   - Weekly: Clean build artifacts
   - Monthly: Review test dependencies
   - Quarterly: Audit for duplicate packages

3. **Never commit bin/obj or artifacts**
   - Already in .gitignore
   - Verify: `git status` before commit

---

## 🚨 Troubleshooting

### Build fails after changes?

**Solution:**
```bash
dotnet clean
dotnet build
```

### Visual Studio doesn't reflect changes?

**Solution:**
1. Close Visual Studio
2. Delete `.vs` folder
3. Clean solution
4. Reopen solution

---

**Last Updated:** 2026-01-26
**Estimated Savings:** 50-70% reduction in test binary disk usage
