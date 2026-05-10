# Hazina Phase 2: Implementation Plan

**Date:** 2026-03-19
**Task:** 869cfzy8b - Hazina Modular Refactoring Phase 2: NuGet Package Strategy
**Status:** Ready for Execution

---

## Overview

This document provides a step-by-step implementation plan for executing the NuGet Package Strategy defined in Phase 2.

**Total Estimated Time:** 8-10 hours
**Dependencies:** Phase 1 (Architecture Audit) ✅ Complete, Phase 4 (.NET Standardization) ✅ Complete

---

## Phase 2 Deliverables

### Completed ✅

1. **Strategy Document** - `docs/PHASE2_NUGET_PACKAGE_STRATEGY.md` (comprehensive 900+ line strategy)
2. **Pack-Local Script** - `scripts/pack-local.ps1` (automated local feed packaging)
3. **Audit Script** - `scripts/audit-package-metadata.ps1` (metadata validation)
4. **Implementation Plan** - This document

### Pending Implementation

5. **Metadata Audit & Updates** - Update 108 .csproj files with complete metadata
6. **Meta-Packages** - Create 6 meta-package .csproj files
7. **Local Feed Setup** - Configure and test local NuGet feed
8. **GitVersion Configuration** - Set up automated versioning
9. **CI/CD Setup** (Optional) - GitHub Actions workflow

---

## Implementation Steps

### Step 1: Initial Audit ✅ COMPLETE

**Status:** Completed 2026-03-19

**Deliverables:**
- [x] Phase 2 Strategy Document (900+ lines)
- [x] `scripts/pack-local.ps1`
- [x] `scripts/audit-package-metadata.ps1`
- [x] This implementation plan

---

### Step 2: Run Metadata Audit

**Objective:** Identify all packages with incomplete or missing metadata

**Duration:** 15 minutes

**Tasks:**

1. **Run audit script:**
   ```powershell
   cd C:\Projects\hazina
   .\scripts\audit-package-metadata.ps1
   ```

2. **Review output:**
   - Count of PASS / WARNINGS / FAILURES
   - Specific issues per project
   - Recommendations

3. **Document findings:**
   - Create `docs/PHASE2_METADATA_AUDIT_RESULTS.md`
   - List all projects with issues
   - Prioritize critical fixes

**Success Criteria:**
- Audit completes without errors
- Clear list of projects needing updates
- Prioritized action plan

---

### Step 3: Update Project Metadata

**Objective:** Ensure all 108 packages have complete NuGet metadata

**Duration:** 3-4 hours

**Tasks:**

1. **Update Required Fields** (Critical)

   For each project missing fields, add to `.csproj`:

   ```xml
   <PropertyGroup>
     <!-- Identity -->
     <PackageId>Hazina.ProjectName</PackageId>
     <Version>1.0.0</Version>

     <!-- Description -->
     <Description>Clear, descriptive summary of what this package does</Description>
     <PackageTags>hazina;ai;llm;relevant-tags</PackageTags>

     <!-- Legal -->
     <Authors>Hazina Team</Authors>
     <PackageLicenseExpression>MIT</PackageLicenseExpression>

     <!-- Links -->
     <RepositoryUrl>https://github.com/martiendejong/Hazina.git</RepositoryUrl>
     <PackageProjectUrl>https://docs.hazina.dev</PackageProjectUrl>
   </PropertyGroup>
   ```

2. **Create README Files**

   For each project without `README.md`:

   **Template:** `docs/templates/PACKAGE_README_TEMPLATE.md`

   ```markdown
   # Hazina.[PackageName]

   [One-line description]

   ## Installation

   ```bash
   dotnet add package Hazina.[PackageName]
   ```

   ## Quick Start

   [Minimal code example]

   ## Features

   - Feature 1
   - Feature 2
   - Feature 3

   ## Documentation

   Full documentation: https://docs.hazina.dev/[package-name]

   ## License

   MIT - see LICENSE file
   ```

3. **Add Package Icon**

   - Create or obtain `icon.png` (128x128 PNG)
   - Place in repository root
   - Verify `Directory.Build.props` includes icon reference

4. **Verify Changes**

   ```powershell
   .\scripts\audit-package-metadata.ps1
   ```

   **Target:** 0 failures, < 10 warnings

**Success Criteria:**
- All packages have PackageId, Version, Description, Authors
- 90%+ packages have README.md
- Audit script shows significant improvement

---

### Step 4: Create Meta-Packages

**Objective:** Create 6 convenience meta-packages for common use cases

**Duration:** 1-2 hours

**Tasks:**

1. **Create Directory Structure:**
   ```powershell
   mkdir src\Meta\Hazina
   mkdir src\Meta\Hazina.Core
   mkdir src\Meta\Hazina.AI.Complete
   mkdir src\Meta\Hazina.Providers.All
   mkdir src\Meta\Hazina.Tools.Complete
   mkdir src\Meta\Hazina.Web
   ```

2. **Create Meta-Package .csproj Files**

   **Reference:** See Appendix C in `PHASE2_NUGET_PACKAGE_STRATEGY.md` for complete example

   **Key Properties:**
   ```xml
   <PropertyGroup>
     <!-- Meta-package: No build output, only dependencies -->
     <IncludeBuildOutput>false</IncludeBuildOutput>
     <SuppressDependenciesWhenPacking>false</SuppressDependenciesWhenPacking>
   </PropertyGroup>

   <ItemGroup>
     <!-- List all included packages -->
     <PackageReference Include="Hazina.LLMs.Client" Version="2.0.0" />
     <PackageReference Include="Hazina.AI.Providers" Version="1.0.0" />
     <!-- ... -->
   </ItemGroup>
   ```

3. **Create README.md for Each Meta-Package**

   Explain what's included and when to use it.

4. **Update Solution Files**

   Add meta-packages to `Hazina.sln`:
   ```powershell
   dotnet sln Hazina.sln add src\Meta\Hazina\Hazina.csproj
   dotnet sln Hazina.sln add src\Meta\Hazina.Core\Hazina.Core.csproj
   # ... (repeat for all 6 meta-packages)
   ```

5. **Test Build**

   ```powershell
   dotnet build src\Meta\Hazina\Hazina.csproj --configuration Release
   ```

**Success Criteria:**
- 6 meta-packages created
- All meta-packages build successfully
- Clear documentation of what each includes

---

### Step 5: Set Up Local NuGet Feed

**Objective:** Configure local NuGet feed for testing before public release

**Duration:** 30 minutes

**Tasks:**

1. **Create Local Feed Directory:**
   ```powershell
   mkdir C:\nuget-local
   ```

2. **Add to NuGet Sources:**
   ```powershell
   dotnet nuget add source C:\nuget-local --name Local
   dotnet nuget list source  # Verify
   ```

3. **Pack All Packages Locally:**
   ```powershell
   .\scripts\pack-local.ps1 -Configuration Release -Version 1.0.0-local
   ```

   **Expected Output:**
   - 108+ packages packed to `C:\nuget-local\`
   - Success count: 108
   - Failed count: 0

4. **Test Meta-Package Installation:**

   Create test project:
   ```powershell
   mkdir C:\Temp\HazinaTest
   cd C:\Temp\HazinaTest
   dotnet new console
   dotnet add package Hazina --source Local --version 1.0.0-local
   dotnet restore
   dotnet build
   ```

   **Expected:** Builds successfully with all dependencies resolved.

5. **Test Provider Installation:**
   ```powershell
   dotnet add package Hazina.LLMs.Anthropic --source Local --version 1.0.0-local
   dotnet restore
   ```

**Success Criteria:**
- Local feed configured and accessible
- All 108+ packages successfully packed
- Meta-packages install correctly
- No dependency resolution errors

---

### Step 6: Configure GitVersion

**Objective:** Automate version calculation from git history

**Duration:** 1 hour

**Tasks:**

1. **Install GitVersion Tool:**
   ```powershell
   dotnet tool install --global GitVersion.Tool
   dotnet tool list -g  # Verify
   ```

2. **Create GitVersion.yml:**

   **File:** `GitVersion.yml` in repository root

   ```yaml
   mode: ContinuousDeployment
   tag-prefix: '[vV]'
   continuous-delivery-fallback-tag: ci
   branches:
     main:
       regex: ^main$
       mode: ContinuousDelivery
       tag: ''
       increment: Patch
       is-mainline: true
     develop:
       regex: ^develop$
       mode: ContinuousDeployment
       tag: alpha
       increment: Minor
     feature:
       regex: ^features?[/-]
       mode: ContinuousDeployment
       tag: beta
       increment: Minor
     release:
       regex: ^releases?[/-]
       mode: ContinuousDeployment
       tag: rc
       increment: Patch
   ```

3. **Test Version Calculation:**
   ```powershell
   cd C:\Projects\hazina
   dotnet-gitversion
   dotnet-gitversion /showvariable SemVer
   ```

   **Expected Output:**
   ```
   2.0.0
   ```

4. **Document Versioning Workflow:**

   Create `docs/VERSIONING_GUIDE.md`:
   - How to tag releases
   - Branch naming conventions
   - Version calculation rules

5. **Test with Pack:**
   ```powershell
   $version = dotnet-gitversion /showvariable SemVer
   dotnet pack src\Core\LLMs\Hazina.LLMs.Client\Hazina.LLMs.Client.csproj `
       --configuration Release `
       --output nupkgs `
       /p:Version=$version
   ```

**Success Criteria:**
- GitVersion installed and working
- Version calculation matches expectations
- Configuration file documented
- Team understands version workflow

---

### Step 7: CI/CD Setup (Optional)

**Objective:** Automate build, pack, and publish on release tags

**Duration:** 2-3 hours

**Tasks:**

1. **Create GitHub Actions Workflow:**

   **File:** `.github/workflows/nuget-publish.yml`

   **Reference:** See CI/CD section in `PHASE2_NUGET_PACKAGE_STRATEGY.md` for complete workflow

2. **Add Secrets to GitHub:**

   - Go to repository Settings → Secrets → Actions
   - Add `NUGET_API_KEY` (from https://www.nuget.org/account/apikeys)

3. **Test Manual Workflow:**

   - Go to Actions tab in GitHub
   - Run "Publish NuGet Packages" workflow manually
   - Provide version: `1.0.0-test`
   - Verify workflow completes successfully

4. **Test Tag-Triggered Workflow:**

   ```powershell
   git checkout main
   git pull
   git tag v1.0.0-rc1
   git push origin v1.0.0-rc1
   ```

   - Verify GitHub Actions triggers
   - Check workflow logs
   - Verify packages NOT published (DryRun mode)

5. **Production Release Test:**

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

   - Verify workflow publishes to NuGet.org
   - Check https://www.nuget.org/packages?q=Hazina
   - Verify all 108+ packages published

**Success Criteria:**
- GitHub Actions workflow configured
- Manual trigger works
- Tag-based trigger works
- Packages publish successfully to NuGet.org

**Note:** This step can be deferred to Phase 5 (Documentation & CI/CD)

---

## Verification & Testing

### Local Testing Checklist

Before publishing to NuGet.org:

- [ ] **Build Success** - `dotnet build Hazina.sln --configuration Release`
- [ ] **Metadata Audit** - `.\scripts\audit-package-metadata.ps1` (0 failures)
- [ ] **Pack Success** - `.\scripts\pack-local.ps1` (108+ success, 0 failures)
- [ ] **Local Installation** - Meta-package installs in test project
- [ ] **Dependency Resolution** - No missing dependencies
- [ ] **Multi-Targeting** - Packages work with net8.0, net9.0, net10.0
- [ ] **Symbol Packages** - `.snupkg` files generated
- [ ] **README Inclusion** - Each package includes README.md
- [ ] **Icon Display** - Package icon shows in NuGet.org UI
- [ ] **Version Consistency** - Related packages have compatible versions

### Sample Projects

**Create 3 test projects:**

1. **Minimal Test** (`Hazina.Core` only)
   ```csharp
   using Hazina.LLMs;
   var client = new MockLLMClient();
   ```

2. **Full Framework Test** (`Hazina` meta-package)
   ```csharp
   using Hazina.LLMs;
   using Hazina.AI;
   var client = new OpenAIClient("key");
   var rag = new RAGPipeline(client);
   ```

3. **Provider Test** (Multiple providers)
   ```csharp
   using Hazina.LLMs.OpenAI;
   using Hazina.LLMs.Anthropic;
   var openai = new OpenAIClient("key1");
   var claude = new AnthropicClient("key2");
   ```

**Expected:** All compile and run without errors.

---

## Rollout Strategy

### Phase A: Internal Testing (1 week)

- Pack to local feed only
- Test with internal projects
- Gather feedback from team
- Fix any issues discovered

### Phase B: Preview Release (1-2 weeks)

- Publish with version `0.9.0-preview`
- Limited external testing
- Gather feedback
- Stabilize APIs

### Phase C: Stable Release (After feedback)

- Publish version `1.0.0`
- Full public announcement
- Documentation site live
- Sample projects published

---

## Risk Mitigation

### Risk 1: Dependency Hell

**Scenario:** Package A and B depend on different versions of Package C

**Mitigation:**
- Use version ranges: `[2.0.0, 3.0.0)`
- Frequent dependency updates
- Central package management (`Directory.Packages.props`)

### Risk 2: Breaking Changes

**Scenario:** Major version bump requires coordinated release of multiple packages

**Mitigation:**
- Semantic versioning discipline
- Pre-release versions for testing (`2.0.0-beta1`)
- Clear migration guides
- Deprecation warnings before removal

### Risk 3: Package Size Bloat

**Scenario:** Meta-packages pull in too many dependencies

**Mitigation:**
- Keep meta-packages focused
- Provide granular options (e.g., `Hazina.Core` vs `Hazina`)
- Document dependency tree
- Exclude unnecessary files from packages

### Risk 4: Version Drift

**Scenario:** Related packages get out of sync

**Mitigation:**
- GitVersion automation
- CI/CD validation
- Version compatibility matrix
- Regular audits

---

## Success Metrics

### Phase 2 Success Criteria

- [x] Strategy document complete (900+ lines)
- [ ] All 108 packages have complete metadata
- [ ] 6 meta-packages created and tested
- [ ] Local feed working
- [ ] GitVersion configured
- [ ] CI/CD pipeline ready (optional)
- [ ] Zero critical issues in audit

### Long-Term Metrics (Post-Release)

- **Download Count** - Track on NuGet.org
- **GitHub Stars** - Repository popularity
- **Issue Count** - Package-related issues
- **Community Feedback** - Surveys, discussions
- **Dependency Graph** - Which packages are most used

---

## Timeline

### Immediate (Week 1)

- [x] Day 1: Strategy document ✅
- [ ] Day 1: Run metadata audit
- [ ] Day 2-3: Update project metadata
- [ ] Day 4: Create meta-packages
- [ ] Day 5: Test local feed

### Near-Term (Week 2)

- [ ] Day 1: GitVersion setup
- [ ] Day 2-3: CI/CD configuration (optional)
- [ ] Day 4: Internal testing
- [ ] Day 5: Documentation updates

### Preview Release (Week 3-4)

- [ ] Publish preview packages
- [ ] Gather feedback
- [ ] Fix issues

### Stable Release (Week 5+)

- [ ] Publish stable 1.0.0
- [ ] Public announcement
- [ ] Documentation site
- [ ] Sample projects

---

## Next Actions

### Immediate Next Steps

1. **Run Audit:**
   ```powershell
   .\scripts\audit-package-metadata.ps1
   ```

2. **Review Results:**
   - Create `docs/PHASE2_METADATA_AUDIT_RESULTS.md`
   - List all projects needing updates

3. **Prioritize Work:**
   - Critical: Missing required fields
   - High: Missing README files
   - Medium: Missing recommended fields
   - Low: Warnings (short descriptions, few tags)

4. **Begin Updates:**
   - Start with Core Foundation packages (12 packages)
   - Then LLM Providers (8 packages)
   - Then AI Core (28 packages)
   - Finally Tools & Infrastructure (60+ packages)

---

## Resources

### Documentation

- **Phase 2 Strategy:** `docs/PHASE2_NUGET_PACKAGE_STRATEGY.md`
- **Phase 1 Audit:** `docs/PHASE1_ARCHITECTURE_AUDIT.md`
- **Phase 4 Standardization:** Git commit `e351c927`

### Scripts

- **Pack Local:** `scripts/pack-local.ps1`
- **Audit Metadata:** `scripts/audit-package-metadata.ps1`
- **Publish NuGet:** `scripts/publish-nuget.ps1` (existing)

### External References

- [Semantic Versioning](https://semver.org/)
- [GitVersion Documentation](https://gitversion.net/)
- [NuGet Package Metadata](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices)
- [Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-your-build)

---

## Conclusion

Phase 2 NuGet Package Strategy is now **fully documented and ready for implementation**. The comprehensive strategy document, automation scripts, and step-by-step plan provide everything needed to transform Hazina from a monorepo into a modular, consumable NuGet package ecosystem.

**Estimated Total Implementation Time:** 8-10 hours (excluding CI/CD)

**Key Deliverables:**
- 108 packages with complete metadata
- 6 meta-packages for convenience
- Local development workflow
- Automated versioning
- CI/CD pipeline (optional)

---

**Document Version:** 1.0
**Last Updated:** 2026-03-19
**Author:** Hazina Team
**Status:** ✅ Ready for Implementation
