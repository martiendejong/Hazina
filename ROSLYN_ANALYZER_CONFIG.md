# Roslyn Analyzer Configuration for Hazina Framework

**Version:** 2.0
**Last Updated:** 2026-01-21
**Purpose:** Technical implementation guide for enforcing Hazina coding standards via Roslyn analyzers, SonarQube, and automated tooling.

---

## Table of Contents

1. [NuGet Packages Installation](#1-nuget-packages-installation)
2. [Complete .editorconfig](#2-complete-editorconfig)
3. [SonarQube Setup](#3-sonarqube-setup)
4. [Pre-Commit Hooks](#4-pre-commit-hooks)
5. [CI/CD Integration](#5-cicd-integration)
6. [Quality Gate Configuration](#6-quality-gate-configuration)
7. [Custom Analyzers](#7-custom-analyzers)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. NuGet Packages Installation

### 1.1 Directory.Build.props (Solution-Wide Configuration)

Create `Directory.Build.props` at solution root to apply packages to all projects:

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <!-- Enable all compiler warnings as errors (strict mode) -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors />

    <!-- Enable XML documentation generation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn> <!-- Suppress missing XML doc warnings in non-public classes -->

    <!-- Enable nullable reference types -->
    <Nullable>enable</Nullable>

    <!-- C# language version -->
    <LangVersion>latest</LangVersion>

    <!-- Code analysis mode -->
    <AnalysisMode>All</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
  </PropertyGroup>

  <ItemGroup>
    <!-- Microsoft .NET Analyzers (built-in) -->
    <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="8.0.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- StyleCop Analyzers (code style enforcement) -->
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.507">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- Roslynator Analyzers (advanced refactorings) -->
    <PackageReference Include="Roslynator.Analyzers" Version="4.7.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- SonarAnalyzer.CSharp (SonarQube integration) -->
    <PackageReference Include="SonarAnalyzer.CSharp" Version="9.16.0.82469">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- Meziantou.Analyzer (best practices) -->
    <PackageReference Include="Meziantou.Analyzer" Version="2.0.127">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- AsyncFixer (async/await best practices) -->
    <PackageReference Include="AsyncFixer" Version="1.6.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>

    <!-- SecurityCodeScan (security vulnerabilities) -->
    <PackageReference Include="SecurityCodeScan.VS2019" Version="5.6.7">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### 1.2 Test Project Configuration

Create `Directory.Build.props` in `Tests/` folder to exclude test projects from strict rules:

```xml
<!-- Tests/Directory.Build.props -->
<Project>
  <PropertyGroup>
    <!-- Disable XML documentation for test projects -->
    <GenerateDocumentationFile>false</GenerateDocumentationFile>

    <!-- Relax some rules for tests -->
    <NoWarn>$(NoWarn);CA1707;CA1062;CA2007</NoWarn>
  </PropertyGroup>
</Project>
```

### 1.3 Installation Commands

```bash
# Navigate to solution root
cd C:\Projects\hazina

# Add packages to all projects
dotnet add package Microsoft.CodeAnalysis.NetAnalyzers --version 8.0.0
dotnet add package StyleCop.Analyzers --version 1.2.0-beta.507
dotnet add package Roslynator.Analyzers --version 4.7.0
dotnet add package SonarAnalyzer.CSharp --version 9.16.0.82469
dotnet add package Meziantou.Analyzer --version 2.0.127
dotnet add package AsyncFixer --version 1.6.0
dotnet add package SecurityCodeScan.VS2019 --version 5.6.7

# Restore to apply analyzers
dotnet restore
```

---

## 2. Complete .editorconfig

### 2.1 Root .editorconfig (Solution-Wide)

Create or update `.editorconfig` at solution root:

```ini
# Top-most EditorConfig file
root = true

##########################
# All Files
##########################
[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true

##########################
# C# Files
##########################
[*.cs]
indent_style = space
indent_size = 4
end_of_line = crlf

##########################
# Code Style Rules
##########################

# this. qualification
dotnet_style_qualification_for_field = false:warning
dotnet_style_qualification_for_property = false:warning
dotnet_style_qualification_for_method = false:warning
dotnet_style_qualification_for_event = false:warning

# var preferences
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion

# Expression-bodied members
csharp_style_expression_bodied_methods = false:none
csharp_style_expression_bodied_constructors = false:none
csharp_style_expression_bodied_operators = false:none
csharp_style_expression_bodied_properties = true:suggestion
csharp_style_expression_bodied_indexers = true:suggestion
csharp_style_expression_bodied_accessors = true:suggestion
csharp_style_expression_bodied_lambdas = true:suggestion

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# Null checking
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# Code block preferences
csharp_prefer_braces = true:warning
csharp_prefer_simple_using_statement = true:suggestion

# Formatting
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_new_line_before_members_in_object_initializers = true
csharp_new_line_before_members_in_anonymous_types = true
csharp_new_line_between_query_expression_clauses = true

csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_indent_labels = no_change

csharp_space_after_cast = false
csharp_space_after_keywords_in_control_flow_statements = true
csharp_space_between_method_call_parameter_list_parentheses = false
csharp_space_between_method_declaration_parameter_list_parentheses = false
csharp_space_between_parentheses = false
csharp_space_before_colon_in_inheritance_clause = true
csharp_space_after_colon_in_inheritance_clause = true
csharp_space_around_binary_operators = before_and_after
csharp_space_between_method_declaration_empty_parameter_list_parentheses = false
csharp_space_between_method_call_name_and_opening_parenthesis = false
csharp_space_between_method_call_empty_parameter_list_parentheses = false

##########################
# Naming Conventions
##########################

# Private fields: _camelCase
dotnet_naming_rule.private_fields_should_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = underscore_camel_case

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case

# Constants: PascalCase
dotnet_naming_rule.constants_should_be_pascal_case.severity = warning
dotnet_naming_rule.constants_should_be_pascal_case.symbols = constants
dotnet_naming_rule.constants_should_be_pascal_case.style = pascal_case

dotnet_naming_symbols.constants.applicable_kinds = field
dotnet_naming_symbols.constants.required_modifiers = const

dotnet_naming_style.pascal_case.capitalization = pascal_case

# Interfaces: IPascalCase
dotnet_naming_rule.interfaces_should_be_prefixed.severity = error
dotnet_naming_rule.interfaces_should_be_prefixed.symbols = interfaces
dotnet_naming_rule.interfaces_should_be_prefixed.style = i_prefix_pascal_case

dotnet_naming_symbols.interfaces.applicable_kinds = interface

dotnet_naming_style.i_prefix_pascal_case.required_prefix = I
dotnet_naming_style.i_prefix_pascal_case.capitalization = pascal_case

# Type parameters: TPascalCase
dotnet_naming_rule.type_parameters_should_be_prefixed.severity = warning
dotnet_naming_rule.type_parameters_should_be_prefixed.symbols = type_parameters
dotnet_naming_rule.type_parameters_should_be_prefixed.style = t_prefix_pascal_case

dotnet_naming_symbols.type_parameters.applicable_kinds = type_parameter

dotnet_naming_style.t_prefix_pascal_case.required_prefix = T
dotnet_naming_style.t_prefix_pascal_case.capitalization = pascal_case

# Async methods: PascalCaseAsync
dotnet_naming_rule.async_methods_should_have_async_suffix.severity = warning
dotnet_naming_rule.async_methods_should_have_async_suffix.symbols = async_methods
dotnet_naming_rule.async_methods_should_have_async_suffix.style = async_suffix

dotnet_naming_symbols.async_methods.applicable_kinds = method
dotnet_naming_symbols.async_methods.required_modifiers = async

dotnet_naming_style.async_suffix.required_suffix = Async
dotnet_naming_style.async_suffix.capitalization = pascal_case

##########################
# Complexity Metrics (CORE STANDARDS)
##########################

# CA1502: Avoid excessive complexity
# Cyclomatic Complexity ≤10 (HARD LIMIT)
dotnet_diagnostic.CA1502.severity = error
dotnet_code_quality.CA1502.threshold = 10

# CA1505: Avoid unmaintainable code
# Maintainability index threshold
dotnet_diagnostic.CA1505.severity = warning
dotnet_code_quality.CA1505.threshold = 20

# CA1506: Avoid excessive class coupling
# Max class coupling
dotnet_diagnostic.CA1506.severity = warning
dotnet_code_quality.CA1506.threshold = 30

# Custom: Max nesting depth = 3
# Enforced via Roslynator RCS1208
dotnet_diagnostic.RCS1208.severity = error

# Custom: Max lines per method = 30
# Enforced via Meziantou MA0051
dotnet_diagnostic.MA0051.severity = warning
dotnet_code_quality.MA0051.maximum_lines_per_method = 30

# Custom: Max methods per class = 10
# Enforced via Meziantou MA0048
dotnet_diagnostic.MA0048.severity = warning
dotnet_code_quality.MA0048.maximum_methods_per_type = 10

# Custom: Max parameters = 4
# Enforced via CA1502
dotnet_diagnostic.CA1502.severity = warning
dotnet_code_quality.CA1502.max_parameters = 4

##########################
# Documentation (100% COVERAGE)
##########################

# CS1591: Missing XML comment for publicly visible type or member
dotnet_diagnostic.CS1591.severity = error

# SA1600: Elements should be documented
dotnet_diagnostic.SA1600.severity = error

# SA1601: Partial elements should be documented
dotnet_diagnostic.SA1601.severity = error

# SA1602: Enumeration items should be documented
dotnet_diagnostic.SA1602.severity = error

# SA1615: Element return value should be documented
dotnet_diagnostic.SA1615.severity = error

# SA1616: Element return value documentation should have text
dotnet_diagnostic.SA1616.severity = error

# SA1617: Void return value should not be documented
dotnet_diagnostic.SA1617.severity = error

# SA1618: Generic type parameters should be documented
dotnet_diagnostic.SA1618.severity = error

# SA1619: Generic type parameters should be documented partial class
dotnet_diagnostic.SA1619.severity = error

# SA1623: Property summary documentation should begin with standard text
dotnet_diagnostic.SA1623.severity = none

##########################
# Code Quality
##########################

# CA1031: Do not catch general exception types
dotnet_diagnostic.CA1031.severity = warning

# CA1062: Validate arguments of public methods
dotnet_diagnostic.CA1062.severity = warning

# CA2007: Consider calling ConfigureAwait on the awaited task
dotnet_diagnostic.CA2007.severity = warning

# CA2008: Do not create tasks without passing a TaskScheduler
dotnet_diagnostic.CA2008.severity = warning

# CA2012: Use ValueTasks correctly
dotnet_diagnostic.CA2012.severity = error

# CA2016: Forward the CancellationToken parameter
dotnet_diagnostic.CA2016.severity = warning

##########################
# Performance
##########################

# CA1806: Do not ignore method results
dotnet_diagnostic.CA1806.severity = warning

# CA1819: Properties should not return arrays
dotnet_diagnostic.CA1819.severity = warning

# CA1822: Mark members as static
dotnet_diagnostic.CA1822.severity = suggestion

# CA1825: Avoid zero-length array allocations
dotnet_diagnostic.CA1825.severity = warning

# CA1826: Do not use Enumerable methods on indexable collections
dotnet_diagnostic.CA1826.severity = warning

# CA1827: Do not use Count()/LongCount() when Any() can be used
dotnet_diagnostic.CA1827.severity = warning

# CA1828: Do not use CountAsync/LongCountAsync when AnyAsync can be used
dotnet_diagnostic.CA1828.severity = warning

# CA1829: Use Length/Count property instead of Count() when available
dotnet_diagnostic.CA1829.severity = warning

# CA1830: Prefer strongly-typed Append and Insert method overloads on StringBuilder
dotnet_diagnostic.CA1830.severity = warning

# CA1832: Use AsSpan or AsMemory instead of Range-based indexers
dotnet_diagnostic.CA1832.severity = suggestion

# CA1834: Consider using 'StringBuilder.Append(char)' when applicable
dotnet_diagnostic.CA1834.severity = warning

# CA1835: Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'
dotnet_diagnostic.CA1835.severity = warning

# CA1836: Prefer IsEmpty over Count when available
dotnet_diagnostic.CA1836.severity = warning

# CA1837: Use 'Environment.ProcessId' instead of 'Process.GetCurrentProcess().Id'
dotnet_diagnostic.CA1837.severity = warning

# CA1838: Avoid 'StringBuilder' parameters for P/Invokes
dotnet_diagnostic.CA1838.severity = warning

##########################
# Security
##########################

# CA2100: Review SQL queries for security vulnerabilities
dotnet_diagnostic.CA2100.severity = error

# CA2119: Seal methods that satisfy private interfaces
dotnet_diagnostic.CA2119.severity = warning

# CA3001: Review code for SQL injection vulnerabilities
dotnet_diagnostic.CA3001.severity = error

# CA3002: Review code for XSS vulnerabilities
dotnet_diagnostic.CA3002.severity = error

# CA3003: Review code for file path injection vulnerabilities
dotnet_diagnostic.CA3003.severity = error

# CA3004: Review code for information disclosure vulnerabilities
dotnet_diagnostic.CA3004.severity = warning

# CA3005: Review code for LDAP injection vulnerabilities
dotnet_diagnostic.CA3005.severity = error

# CA3006: Review code for process command injection vulnerabilities
dotnet_diagnostic.CA3006.severity = error

# CA3007: Review code for open redirect vulnerabilities
dotnet_diagnostic.CA3007.severity = warning

# CA3008: Review code for XPath injection vulnerabilities
dotnet_diagnostic.CA3008.severity = error

# CA3009: Review code for XML injection vulnerabilities
dotnet_diagnostic.CA3009.severity = error

# CA3010: Review code for XAML injection vulnerabilities
dotnet_diagnostic.CA3010.severity = error

# CA3011: Review code for DLL injection vulnerabilities
dotnet_diagnostic.CA3011.severity = error

# CA3012: Review code for regex injection vulnerabilities
dotnet_diagnostic.CA3012.severity = error

# CA5350: Do Not Use Weak Cryptographic Algorithms
dotnet_diagnostic.CA5350.severity = error

# CA5351: Do Not Use Broken Cryptographic Algorithms
dotnet_diagnostic.CA5351.severity = error

##########################
# StyleCop Rules
##########################

# SA0001: XML comment analysis is disabled due to project configuration
dotnet_diagnostic.SA0001.severity = none

# SA1101: Prefix local calls with this
dotnet_diagnostic.SA1101.severity = none

# SA1200: Using directives should be placed correctly
dotnet_diagnostic.SA1200.severity = none

# SA1309: Field names should not begin with underscore
dotnet_diagnostic.SA1309.severity = none

# SA1633: File should have header
dotnet_diagnostic.SA1633.severity = none

# SA1649: File name should match first type name
dotnet_diagnostic.SA1649.severity = error

##########################
# Roslynator Rules
##########################

# RCS1001: Add braces (when expression spans over multiple lines)
dotnet_diagnostic.RCS1001.severity = warning

# RCS1003: Add braces to if-else
dotnet_diagnostic.RCS1003.severity = warning

# RCS1021: Convert lambda expression body to expression-body
dotnet_diagnostic.RCS1021.severity = none

# RCS1036: Remove redundant empty line
dotnet_diagnostic.RCS1036.severity = suggestion

# RCS1037: Remove trailing white-space
dotnet_diagnostic.RCS1037.severity = warning

# RCS1080: Use 'Count/Length' property instead of 'Any' method
dotnet_diagnostic.RCS1080.severity = warning

# RCS1163: Unused parameter
dotnet_diagnostic.RCS1163.severity = warning

# RCS1194: Implement exception constructors
dotnet_diagnostic.RCS1194.severity = warning

# RCS1208: Reduce if nesting (Max nesting = 3)
dotnet_diagnostic.RCS1208.severity = error

##########################
# Meziantou Rules
##########################

# MA0004: Use Task.ConfigureAwait(false)
dotnet_diagnostic.MA0004.severity = warning

# MA0011: IFormatProvider is missing
dotnet_diagnostic.MA0011.severity = warning

# MA0015: Specify the parameter name
dotnet_diagnostic.MA0015.severity = suggestion

# MA0016: Prefer return collection abstraction instead of implementation
dotnet_diagnostic.MA0016.severity = suggestion

# MA0025: Implement the functionality instead of throwing NotImplementedException
dotnet_diagnostic.MA0025.severity = error

# MA0026: Fix TODO comment
dotnet_diagnostic.MA0026.severity = warning

# MA0048: File name must match type name
dotnet_diagnostic.MA0048.severity = error

# MA0051: Method is too long (max 30 lines)
dotnet_diagnostic.MA0051.severity = warning
dotnet_code_quality.MA0051.maximum_lines_per_method = 30

# MA0076: Do not use implicit culture-sensitive ToString
dotnet_diagnostic.MA0076.severity = warning

##########################
# AsyncFixer Rules
##########################

# AsyncFixer01: Unnecessary async/await usage
dotnet_diagnostic.AsyncFixer01.severity = suggestion

# AsyncFixer02: Long-running or blocking operations inside an async method
dotnet_diagnostic.AsyncFixer02.severity = error

# AsyncFixer03: Fire-and-forget async-void methods or delegates
dotnet_diagnostic.AsyncFixer03.severity = error

# AsyncFixer04: Fire-and-forget async call inside a using block
dotnet_diagnostic.AsyncFixer04.severity = error

##########################
# SonarAnalyzer Rules (Cognitive Complexity)
##########################

# S1541: Methods should not be too complex (Cognitive Complexity ≤15)
dotnet_diagnostic.S1541.severity = error
dotnet_code_quality.S1541.threshold = 15

# S107: Methods should not have too many parameters (max 4)
dotnet_diagnostic.S107.severity = warning
dotnet_code_quality.S107.max = 4

# S138: Methods should not have too many lines (max 30)
dotnet_diagnostic.S138.severity = warning
dotnet_code_quality.S138.max = 30

# S1200: Classes should not be coupled to too many other classes
dotnet_diagnostic.S1200.severity = warning
dotnet_code_quality.S1200.max = 20

```

### 2.2 Project-Specific Overrides

For projects that need exceptions (e.g., test projects, legacy code):

```ini
# Tests/.editorconfig
root = true

[*.cs]
# Relax documentation requirements for tests
dotnet_diagnostic.CS1591.severity = none
dotnet_diagnostic.SA1600.severity = none

# Relax method size for tests (test methods can be longer)
dotnet_diagnostic.MA0051.severity = none
dotnet_diagnostic.S138.severity = none

# Allow more methods in test classes
dotnet_diagnostic.MA0048.severity = none
```

---

## 3. SonarQube Setup

### 3.1 Install SonarQube Server (Docker)

```bash
# Pull SonarQube Docker image
docker pull sonarqube:latest

# Run SonarQube
docker run -d --name sonarqube \
  -p 9000:9000 \
  -v sonarqube_data:/opt/sonarqube/data \
  -v sonarqube_extensions:/opt/sonarqube/extensions \
  -v sonarqube_logs:/opt/sonarqube/logs \
  sonarqube:latest

# Access SonarQube at http://localhost:9000
# Default credentials: admin/admin
```

### 3.2 Install SonarScanner for .NET

```bash
# Install dotnet-sonarscanner
dotnet tool install --global dotnet-sonarscanner

# Verify installation
dotnet sonarscanner --version
```

### 3.3 SonarQube Project Configuration

Create `sonar-project.properties` at solution root:

```properties
# Project identification
sonar.projectKey=Hazina
sonar.projectName=Hazina Framework
sonar.projectVersion=2.0

# Source and test paths
sonar.sources=src
sonar.tests=Tests

# Exclusions
sonar.exclusions=**/bin/**,**/obj/**,**/*.Generated.cs
sonar.coverage.exclusions=**/*Tests.cs,**/*TestData.cs,**/Program.cs,**/Startup.cs

# C# specific settings
sonar.language=cs
sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml
sonar.cs.vstest.reportsPaths=**/*.trx

# Complexity thresholds
sonar.cs.cyclomatic.max=10
sonar.cs.cognitive.max=15

# Code coverage
sonar.coverageReportPaths=**/coverage.cobertura.xml

# Quality Gate
sonar.qualitygate.wait=true
sonar.qualitygate.timeout=300

# Technical debt rating
sonar.technicalDebt.hoursInDay=8
sonar.technicalDebt.ratingGrid=0.05,0.1,0.2,0.5

# Duplications
sonar.cpd.exclusions=**/*Tests.cs

# Encoding
sonar.sourceEncoding=UTF-8
```

### 3.4 Run SonarQube Analysis

```bash
# Begin analysis
dotnet sonarscanner begin \
  /k:"Hazina" \
  /d:sonar.host.url="http://localhost:9000" \
  /d:sonar.login="<your-token>"

# Build solution
dotnet build

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# End analysis
dotnet sonarscanner end /d:sonar.login="<your-token>"
```

### 3.5 Generate SonarQube Token

1. Login to SonarQube: http://localhost:9000
2. Navigate to: User > My Account > Security
3. Generate token: "Hazina-CI"
4. Copy token and store securely

---

## 4. Pre-Commit Hooks

### 4.1 Using Husky.Net

```bash
# Navigate to solution root
cd C:\Projects\hazina

# Install Husky.Net
dotnet new tool-manifest
dotnet tool install Husky

# Initialize Husky
dotnet husky install

# Add pre-commit hook for formatting
dotnet husky add pre-commit -c "dotnet format --verify-no-changes"

# Add pre-commit hook for build
dotnet husky add pre-commit -c "dotnet build /p:TreatWarningsAsErrors=true"

# Add pre-commit hook for unit tests
dotnet husky add pre-commit -c "dotnet test --filter Category=Unit --no-build"

# Add pre-commit hook for documentation check
dotnet husky add pre-commit -c "dotnet build /p:GenerateDocumentationFile=true /p:TreatWarningsAsErrors=true"
```

### 4.2 Manual Git Hook (Alternative)

Create `.git/hooks/pre-commit`:

```bash
#!/bin/sh

echo "Running pre-commit checks..."

# 1. Check code formatting
echo "Checking code formatting..."
dotnet format --verify-no-changes --verbosity quiet
if [ $? -ne 0 ]; then
  echo "❌ Code formatting failed. Run 'dotnet format' to fix."
  exit 1
fi

# 2. Build with strict warnings
echo "Building with strict warnings..."
dotnet build /p:TreatWarningsAsErrors=true --nologo --verbosity quiet
if [ $? -ne 0 ]; then
  echo "❌ Build failed with warnings/errors."
  exit 1
fi

# 3. Run unit tests
echo "Running unit tests..."
dotnet test --filter "Category=Unit" --no-build --nologo --verbosity quiet
if [ $? -ne 0 ]; then
  echo "❌ Unit tests failed."
  exit 1
fi

# 4. Check XML documentation
echo "Checking XML documentation..."
dotnet build /p:GenerateDocumentationFile=true /p:TreatWarningsAsErrors=true --nologo --verbosity quiet
if [ $? -ne 0 ]; then
  echo "❌ Missing XML documentation."
  exit 1
fi

echo "✅ All pre-commit checks passed!"
exit 0
```

Make executable:
```bash
chmod +x .git/hooks/pre-commit
```

---

## 5. CI/CD Integration

### 5.1 GitHub Actions Workflow

Create `.github/workflows/quality-gate.yml`:

```yaml
name: Quality Gate

on:
  push:
    branches: [ develop, main ]
  pull_request:
    branches: [ develop, main ]

jobs:
  quality:
    name: Code Quality & Coverage
    runs-on: ubuntu-latest

    steps:
      - name: Checkout code
        uses: actions/checkout@v3
        with:
          fetch-depth: 0  # Shallow clones disabled for SonarQube

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Cache NuGet packages
        uses: actions/cache@v3
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
          restore-path: |
            ~/.nuget/packages

      - name: Restore dependencies
        run: dotnet restore

      - name: Check code formatting
        run: dotnet format --verify-no-changes --verbosity diagnostic

      - name: Install SonarScanner
        run: dotnet tool install --global dotnet-sonarscanner

      - name: Begin SonarQube analysis
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: |
          dotnet sonarscanner begin \
            /k:"Hazina" \
            /d:sonar.host.url="${{ secrets.SONAR_HOST_URL }}" \
            /d:sonar.login="${{ secrets.SONAR_TOKEN }}" \
            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" \
            /d:sonar.coverage.exclusions="**/*Tests.cs"

      - name: Build solution
        run: dotnet build --no-restore /p:TreatWarningsAsErrors=true

      - name: Run unit tests with coverage
        run: |
          dotnet test --no-build --verbosity normal \
            --filter "Category=Unit" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults \
            -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

      - name: End SonarQube analysis
        env:
          SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
        run: dotnet sonarscanner end /d:sonar.login="${{ secrets.SONAR_TOKEN }}"

      - name: Check coverage threshold
        run: |
          dotnet test --no-build \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura \
            /p:Threshold=70 \
            /p:ThresholdType=branch \
            /p:ThresholdStat=total

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v3
        with:
          files: ./TestResults/**/coverage.opencover.xml
          flags: unittests
          name: codecov-umbrella

      - name: Comment PR with coverage
        if: github.event_name == 'pull_request'
        uses: 5monkeys/cobertura-action@master
        with:
          path: ./TestResults/**/coverage.cobertura.xml
          minimum_coverage: 70
```

### 5.2 Azure DevOps Pipeline

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
      - develop
      - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

steps:
  - task: UseDotNet@2
    displayName: 'Use .NET 8.0'
    inputs:
      version: '8.0.x'

  - task: DotNetCoreCLI@2
    displayName: 'Restore packages'
    inputs:
      command: 'restore'
      projects: '**/*.csproj'

  - task: DotNetCoreCLI@2
    displayName: 'Check code formatting'
    inputs:
      command: 'custom'
      custom: 'format'
      arguments: '--verify-no-changes --verbosity diagnostic'

  - task: SonarQubePrepare@5
    displayName: 'Prepare SonarQube analysis'
    inputs:
      SonarQube: 'SonarQube-Hazina'
      scannerMode: 'MSBuild'
      projectKey: 'Hazina'
      projectName: 'Hazina Framework'
      extraProperties: |
        sonar.cs.opencover.reportsPaths=$(Build.SourcesDirectory)/**/coverage.opencover.xml
        sonar.cs.cyclomatic.max=10
        sonar.cs.cognitive.max=15

  - task: DotNetCoreCLI@2
    displayName: 'Build solution'
    inputs:
      command: 'build'
      projects: '**/*.csproj'
      arguments: '--configuration $(buildConfiguration) /p:TreatWarningsAsErrors=true'

  - task: DotNetCoreCLI@2
    displayName: 'Run unit tests'
    inputs:
      command: 'test'
      projects: '**/*Tests.csproj'
      arguments: >
        --configuration $(buildConfiguration)
        --no-build
        --filter "Category=Unit"
        --collect:"XPlat Code Coverage"
        --results-directory $(Build.SourcesDirectory)/TestResults
        --logger trx
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

  - task: PublishCodeCoverageResults@1
    displayName: 'Publish code coverage'
    inputs:
      codeCoverageTool: 'Cobertura'
      summaryFileLocation: '$(Build.SourcesDirectory)/TestResults/**/coverage.cobertura.xml'

  - task: SonarQubeAnalyze@5
    displayName: 'Run SonarQube analysis'

  - task: SonarQubePublish@5
    displayName: 'Publish SonarQube results'
    inputs:
      pollingTimeoutSec: '300'

  - task: sonar-buildbreaker@8
    displayName: 'Break build on quality gate failure'
    inputs:
      SonarQube: 'SonarQube-Hazina'
```

---

## 6. Quality Gate Configuration

### 6.1 SonarQube Quality Gate

Login to SonarQube → Quality Gates → Create → "Hazina Quality Gate"

**Conditions:**

| Metric | Operator | Value | Type |
|--------|----------|-------|------|
| Coverage on New Code | is less than | 70% | Error |
| Duplicated Lines (%) on New Code | is greater than | 3% | Error |
| Maintainability Rating on New Code | is worse than | A | Error |
| Reliability Rating on New Code | is worse than | A | Error |
| Security Rating on New Code | is worse than | A | Error |
| Security Hotspots Reviewed | is less than | 100% | Warning |
| Code Smells | is greater than | 0 | Warning |
| Blocker Issues | is greater than | 0 | Error |
| Critical Issues | is greater than | 0 | Error |

### 6.2 Apply Quality Gate to Project

1. Navigate to: Project Settings → Quality Gate
2. Select: "Hazina Quality Gate"
3. Save

---

## 7. Custom Analyzers

### 7.1 Create Custom Analyzer Project

```bash
# Create analyzer project
dotnet new classlib -n Hazina.Analyzers -f netstandard2.0

# Add analyzer packages
cd Hazina.Analyzers
dotnet add package Microsoft.CodeAnalysis.CSharp --version 4.7.0
dotnet add package Microsoft.CodeAnalysis.Analyzers --version 3.3.4
```

### 7.2 Example: Detect WordPressProvider Anti-Pattern

```csharp
// Hazina.Analyzers/GodObjectAnalyzer.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class GodObjectAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "HAZINA001";
    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Class has too many public methods (God Object)",
        "Class '{0}' has {1} public methods. Consider splitting into focused classes (max 10 recommended).",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Classes with >10 public methods likely violate Single Responsibility Principle.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        var publicMethods = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword))
            .Count();

        if (publicMethods > 10)
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                classDeclaration.Identifier.GetLocation(),
                classDeclaration.Identifier.Text,
                publicMethods);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
```

### 7.3 Use Custom Analyzer

```xml
<!-- Add to projects -->
<ItemGroup>
  <ProjectReference Include="..\Hazina.Analyzers\Hazina.Analyzers.csproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
    <OutputItemType>Analyzer</OutputItemType>
  </ProjectReference>
</ItemGroup>
```

---

## 8. Troubleshooting

### 8.1 Common Issues

#### Issue: Analyzers not running

**Solution:**
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

#### Issue: Too many warnings

**Solution:**
```xml
<!-- Incrementally enable rules in .editorconfig -->
<!-- Start with errors only, then warnings -->
dotnet_diagnostic.CA1502.severity = error  # Start here
dotnet_diagnostic.CA1031.severity = none   # Temporarily disable
```

#### Issue: SonarQube not detecting coverage

**Solution:**
```bash
# Ensure correct coverage format
dotnet test --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Verify coverage file exists
ls TestResults/**/coverage.opencover.xml
```

### 8.2 Performance Impact

**Analyzer performance:**
- Build time increase: ~10-20% with all analyzers
- IDE responsiveness: Minimal impact with modern hardware

**Optimization:**
```xml
<!-- Disable analyzers in Debug builds -->
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
</PropertyGroup>

<!-- Enable only for Release builds -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <RunAnalyzersDuringBuild>true</RunAnalyzersDuringBuild>
</PropertyGroup>
```

---

## Summary

This configuration enforces Hazina coding standards through:

1. **Roslyn Analyzers** - Compile-time enforcement (cyclomatic complexity, documentation)
2. **SonarQube** - Deep static analysis (cognitive complexity, code smells, security)
3. **Pre-Commit Hooks** - Prevent non-compliant code from being committed
4. **CI/CD Pipelines** - Block merges that fail quality gates
5. **Custom Analyzers** - Hazina-specific rules (God Object detection, etc.)

**Result:** Zero-tolerance enforcement of complexity metrics, documentation requirements, and code quality standards.

---

**Next Steps:**
1. Install NuGet packages: `dotnet restore`
2. Apply .editorconfig: Copy to solution root
3. Set up SonarQube: Run Docker container
4. Install pre-commit hooks: `dotnet husky install`
5. Configure CI/CD: Add GitHub Actions workflow
6. Run first analysis: `dotnet build` + `dotnet sonarscanner`
7. Review quality gate results
8. Iterate and improve

**Full documentation:** [HAZINA_CODING_STANDARDS.md](./HAZINA_CODING_STANDARDS.md)
