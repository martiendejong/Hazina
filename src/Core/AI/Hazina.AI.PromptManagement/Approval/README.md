# Approval Workflow

The Approval Workflow system manages human oversight and approval of AI-generated prompt changes. It provides intelligent auto-approval for low-risk changes while requiring human review for high-risk modifications.

## Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                      APPROVAL WORKFLOW                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Proposal      Risk         Auto-Approval      Human           │
│  Submission -> Assessment -> Decision      ->  Review (if req) │
│                                                                 │
│  Components:                                                    │
│  • Risk Level Calculation                                      │
│  • Auto-Approval Eligibility Check                             │
│  • Approval Request Management                                 │
│  • Approver Assignment & Notification                          │
│  • Approval/Rejection Tracking                                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Key Features

### 1. **Intelligent Auto-Approval**
Automatically approves low-risk changes that meet safety criteria:
- Semantic similarity > 90% (max 10% change)
- Performance improvement > 5%
- Safety validation passed
- Template length change < 30%

### 2. **Risk-Based Approval Routing**
- **Low Risk**: Auto-approved if enabled
- **Medium Risk**: Assigned to designated approver
- **High Risk**: Requires senior approver
- **Critical Risk**: Requires multiple approvals

### 3. **Flexible Configuration**
Per-prompt approval policies:
- Enable/disable approval requirement
- Configure auto-approval thresholds
- Assign specific approvers
- Set approval timeouts
- Configure notifications

### 4. **Complete Audit Trail**
Track all approval decisions:
- Who submitted
- Who reviewed
- When reviewed
- Approval/rejection reason
- Auto-approval details

## Architecture

### Core Components

```
IApprovalWorkflow (Interface)
├── SubmitProposalAsync()         - Submit proposal for approval
├── GetRequestAsync()              - Retrieve approval request
├── GetPendingRequestsAsync()      - List pending approvals
├── ApproveAsync()                 - Approve a proposal
├── RejectAsync()                  - Reject a proposal
├── CancelAsync()                  - Cancel a pending request
├── CheckAutoApprovalAsync()       - Check auto-approval eligibility
├── GetConfigAsync()               - Get approval configuration
└── SaveConfigAsync()              - Save approval configuration

ApprovalWorkflow (Implementation)
├── Risk Assessment Logic
├── Auto-Approval Decision Engine
├── Approver Assignment
└── Notification System (TODO)

IApprovalStore (Storage Interface)
├── SaveRequestAsync()
├── GetRequestAsync()
├── GetRequestByProposalAsync()
├── GetPendingRequestsAsync()
├── GetRequestsForPromptAsync()
├── DeleteOldRequestsAsync()
├── GetConfigAsync()
├── SaveConfigAsync()
└── DeleteConfigAsync()

PostgresApprovalStore (PostgreSQL Implementation)
```

## Data Model

### ApprovalRequest
```csharp
public class ApprovalRequest
{
    public string RequestId { get; set; }
    public string ProposalId { get; set; }
    public string PromptId { get; set; }
    public ApprovalStatus Status { get; set; }        // Pending, Approved, Rejected, Cancelled, Expired
    public RiskLevel RiskLevel { get; set; }          // Low, Medium, High, Critical
    public string SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string? AssignedTo { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? Comments { get; set; }
    public string? RejectionReason { get; set; }
    public bool AutoApproved { get; set; }
    public string? AutoApprovalReason { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### ApprovalConfig
```csharp
public class ApprovalConfig
{
    public string PromptId { get; set; }
    public bool RequireApproval { get; set; }                      // Default: true
    public bool AllowAutoApproval { get; set; }                    // Default: true
    public double AutoApprovalMaxSemanticChange { get; set; }      // Default: 0.10 (10%)
    public double AutoApprovalMinPerformance { get; set; }         // Default: 1.05 (5% improvement)
    public List<string> ApproverUserIds { get; set; }
    public List<string> ApproverRoles { get; set; }
    public int RequiredApprovals { get; set; }                     // Default: 1
    public TimeSpan ApprovalTimeout { get; set; }                  // Default: 7 days
    public bool NotifyOnSubmission { get; set; }                   // Default: true
    public List<string> NotificationChannels { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### AutoApprovalDecision
```csharp
public class AutoApprovalDecision
{
    public bool CanAutoApprove { get; set; }
    public string Reason { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public List<string> RiskFactors { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

## Usage Examples

### 1. Submit Proposal for Approval

```csharp
var workflow = new ApprovalWorkflow(store, rewriter, safetyCoordinator);

// Submit a proposal
var request = await workflow.SubmitProposalAsync(
    proposal: myProposal,
    submittedBy: "user@example.com",
    cancellationToken: cancellationToken
);

if (request.AutoApproved)
{
    Console.WriteLine($"Auto-approved: {request.AutoApprovalReason}");
}
else
{
    Console.WriteLine($"Requires approval from: {request.AssignedTo}");
    Console.WriteLine($"Risk level: {request.RiskLevel}");
}
```

### 2. Check Auto-Approval Eligibility

```csharp
// Check if a proposal can be auto-approved
var decision = await workflow.CheckAutoApprovalAsync(
    proposal: myProposal,
    cancellationToken: cancellationToken
);

Console.WriteLine($"Can auto-approve: {decision.CanAutoApprove}");
Console.WriteLine($"Risk level: {decision.RiskLevel}");
Console.WriteLine($"Reason: {decision.Reason}");

if (decision.RiskFactors.Any())
{
    Console.WriteLine("Risk factors:");
    foreach (var factor in decision.RiskFactors)
    {
        Console.WriteLine($"  - {factor}");
    }
}
```

### 3. Get Pending Approvals

```csharp
// Get all pending requests for an approver
var pendingRequests = await workflow.GetPendingRequestsAsync(
    approverId: "approver@example.com",
    cancellationToken: cancellationToken
);

Console.WriteLine($"You have {pendingRequests.Count} pending approvals:");
foreach (var req in pendingRequests)
{
    Console.WriteLine($"  Request {req.RequestId}:");
    Console.WriteLine($"    Prompt: {req.PromptId}");
    Console.WriteLine($"    Risk: {req.RiskLevel}");
    Console.WriteLine($"    Submitted: {req.SubmittedAt} by {req.SubmittedBy}");
}
```

### 4. Approve a Proposal

```csharp
// Approve a proposal
var approvedRequest = await workflow.ApproveAsync(
    requestId: "req-12345",
    approvedBy: "approver@example.com",
    comments: "Looks good. Performance improvement is significant.",
    cancellationToken: cancellationToken
);

Console.WriteLine($"Approved at: {approvedRequest.ReviewedAt}");
```

### 5. Reject a Proposal

```csharp
// Reject a proposal
var rejectedRequest = await workflow.RejectAsync(
    requestId: "req-12345",
    rejectedBy: "approver@example.com",
    reason: "Semantic drift too high. Rewording changes core intent.",
    cancellationToken: cancellationToken
);

Console.WriteLine($"Rejected: {rejectedRequest.RejectionReason}");
```

### 6. Configure Approval Policy

```csharp
// Configure approval policy for a prompt
var config = new ApprovalConfig
{
    PromptId = "customer-support-agent",
    RequireApproval = true,
    AllowAutoApproval = true,
    AutoApprovalMaxSemanticChange = 0.05,  // Max 5% change for auto-approval
    AutoApprovalMinPerformance = 1.10,      // Require 10% improvement
    ApproverUserIds = new List<string>
    {
        "senior-pm@example.com",
        "eng-lead@example.com"
    },
    RequiredApprovals = 1,
    ApprovalTimeout = TimeSpan.FromDays(3),
    NotifyOnSubmission = true,
    NotificationChannels = new List<string> { "email", "slack" }
};

await workflow.SaveConfigAsync(config, cancellationToken);
```

### 7. Disable Approval for Low-Risk Prompts

```csharp
// For internal tools or low-risk prompts, disable approval requirement
var config = new ApprovalConfig
{
    PromptId = "internal-logging-formatter",
    RequireApproval = false,  // No approval needed
    AllowAutoApproval = true,
    Metadata = new Dictionary<string, object>
    {
        ["reason"] = "Low-risk internal tool"
    }
};

await workflow.SaveConfigAsync(config, cancellationToken);
```

## Auto-Approval Logic

The system calculates risk level based on multiple factors:

### Risk Factors Checked

1. **Semantic Change**
   - Measures how much the prompt meaning has changed
   - Calculated as: `1.0 - SemanticSimilarity`
   - Default threshold: 10% max change

2. **Performance Impact**
   - Requires minimum performance improvement
   - Default threshold: 5% improvement
   - Performance regression → High Risk

3. **Safety Validation**
   - Must pass safety coordinator checks
   - Missing validation → Medium Risk
   - Failed validation → High Risk

4. **Template Length Change**
   - Large length changes indicate significant rewrites
   - Threshold: 30% length change

### Risk Level Determination

```
Low Risk:
  ✓ Semantic change ≤ 10%
  ✓ Performance improvement ≥ 5%
  ✓ Safety validation passed
  ✓ Length change ≤ 30%
  → Auto-approved (if enabled)

Medium Risk:
  ⚠ Semantic change > 10%
  OR ⚠ Performance improvement < 5%
  OR ⚠ No safety validation
  OR ⚠ Length change > 30%
  → Requires human review

High Risk:
  ⛔ Performance regression
  OR ⛔ Safety validation failed
  → Requires senior approval

Critical Risk:
  🚨 Multiple high-risk factors
  → Requires multiple approvals
```

## Database Schema

### approval_requests Table
```sql
CREATE TABLE approval_requests (
    request_id VARCHAR(255) PRIMARY KEY,
    proposal_id VARCHAR(255) NOT NULL REFERENCES prompt_proposals(proposal_id),
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id),
    status VARCHAR(50) NOT NULL,
    risk_level VARCHAR(50) NOT NULL,
    submitted_by VARCHAR(255) NOT NULL,
    submitted_at TIMESTAMP NOT NULL,
    assigned_to VARCHAR(255),
    reviewed_by VARCHAR(255),
    reviewed_at TIMESTAMP,
    comments TEXT,
    rejection_reason TEXT,
    auto_approved BOOLEAN DEFAULT false,
    auto_approval_reason TEXT,
    metadata JSONB
);
```

### approval_configs Table
```sql
CREATE TABLE approval_configs (
    prompt_id VARCHAR(255) PRIMARY KEY REFERENCES prompt_templates(prompt_id),
    require_approval BOOLEAN DEFAULT true,
    allow_auto_approval BOOLEAN DEFAULT true,
    auto_approval_max_semantic_change FLOAT DEFAULT 0.10,
    auto_approval_min_performance FLOAT DEFAULT 1.05,
    approver_user_ids JSONB,
    approver_roles JSONB,
    required_approvals INT DEFAULT 1,
    approval_timeout INTERVAL DEFAULT '7 days',
    notify_on_submission BOOLEAN DEFAULT true,
    notification_channels JSONB,
    metadata JSONB
);
```

## Integration with Other Components

### With Safety Coordinator
```csharp
// Approval workflow uses safety coordinator for validation
var safetyResult = await _safetyCoordinator.ValidateProposalAsync(
    proposal,
    cancellationToken
);

// Safety validation affects risk level
if (!safetyResult.Passed)
{
    riskLevel = RiskLevel.High;
}
```

### With Prompt Rewriter
```csharp
// Uses rewriter for semantic similarity calculation
var similarity = await _rewriter.CalculateSimilarityAsync(
    originalTemplate: proposal.OriginalTemplate,
    proposedTemplate: proposal.ProposedTemplate,
    cancellationToken: cancellationToken
);

// Similarity affects auto-approval decision
var semanticChange = 1.0 - similarity;
if (semanticChange > config.AutoApprovalMaxSemanticChange)
{
    requiresApproval = true;
}
```

## Workflow States

```
┌──────────┐
│ Proposal │
│ Created  │
└─────┬────┘
      │
      ▼
┌──────────────────┐
│ Submit for       │
│ Approval         │
└─────┬────────────┘
      │
      ▼
┌──────────────────┐     ┌─────────────┐
│ Risk Assessment  │────→│ Low Risk    │
└─────┬────────────┘     │ + Enabled   │
      │                  └──────┬──────┘
      │                         │
      │                         ▼
      │                  ┌──────────────┐
      │                  │ AUTO-APPROVED│
      │                  └──────────────┘
      │
      ▼
┌──────────────────┐
│ PENDING          │
│ (Assigned)       │
└─────┬────────────┘
      │
      ├──────────┬──────────┬──────────┐
      ▼          ▼          ▼          ▼
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│ APPROVED │ │ REJECTED │ │CANCELLED │ │ EXPIRED  │
└──────────┘ └──────────┘ └──────────┘ └──────────┘
```

## Best Practices

### 1. Configure Appropriate Thresholds
```csharp
// For critical production prompts
var strictConfig = new ApprovalConfig
{
    PromptId = "payment-processing-agent",
    AutoApprovalMaxSemanticChange = 0.05,  // Very conservative
    AutoApprovalMinPerformance = 1.15,      // Require significant improvement
    RequiredApprovals = 2                   // Multiple reviewers
};

// For experimental prompts
var lenientConfig = new ApprovalConfig
{
    PromptId = "experimental-chat-bot",
    AutoApprovalMaxSemanticChange = 0.20,  // Allow more change
    AutoApprovalMinPerformance = 1.00,      // Just need to not regress
    RequiredApprovals = 1
};
```

### 2. Assign Multiple Approvers
```csharp
var config = new ApprovalConfig
{
    PromptId = "my-prompt",
    ApproverUserIds = new List<string>
    {
        "pm@example.com",
        "tech-lead@example.com",
        "product-owner@example.com"
    },
    RequiredApprovals = 1  // Any one can approve
};
```

### 3. Use Role-Based Approvers
```csharp
var config = new ApprovalConfig
{
    PromptId = "customer-facing-prompt",
    ApproverRoles = new List<string>
    {
        "product-manager",
        "senior-engineer",
        "ai-safety-reviewer"
    }
};
```

### 4. Set Appropriate Timeouts
```csharp
// Short timeout for fast-moving projects
var agileConfig = new ApprovalConfig
{
    ApprovalTimeout = TimeSpan.FromDays(2)
};

// Longer timeout for enterprise review process
var enterpriseConfig = new ApprovalConfig
{
    ApprovalTimeout = TimeSpan.FromDays(14)
};
```

### 5. Monitor Approval Metrics
```csharp
// Get approval statistics
var allRequests = await store.GetRequestsForPromptAsync(promptId);

var autoApprovedCount = allRequests.Count(r => r.AutoApproved);
var manualApprovedCount = allRequests.Count(r => !r.AutoApproved && r.Status == ApprovalStatus.Approved);
var rejectedCount = allRequests.Count(r => r.Status == ApprovalStatus.Rejected);

var autoApprovalRate = (double)autoApprovedCount / allRequests.Count;
Console.WriteLine($"Auto-approval rate: {autoApprovalRate:P1}");
```

## Security Considerations

### 1. Authorization
The approval workflow does NOT handle authentication/authorization. You must implement:
- User authentication before calling approval methods
- Role-based access control (RBAC) to verify approver permissions
- Audit logging of all approval actions

### 2. Approver Verification
```csharp
// Verify approver has permission before approving
if (!await IsAuthorizedApprover(userId, request.PromptId))
{
    throw new UnauthorizedAccessException("User is not authorized to approve this prompt");
}

await workflow.ApproveAsync(request.RequestId, userId, comments);
```

### 3. Prevent Self-Approval
```csharp
// Don't allow users to approve their own submissions
if (request.SubmittedBy == currentUserId)
{
    throw new InvalidOperationException("Cannot approve your own submission");
}
```

## Performance Considerations

### 1. Index Usage
The PostgreSQL implementation includes indexes on:
- `status` - Fast filtering of pending requests
- `assigned_to` - Fast lookup of approver's queue
- `prompt_id` - Fast lookup of approval history
- `submitted_at` - Fast chronological sorting

### 2. Cleanup Old Requests
```csharp
// Periodically clean up old completed requests
var deletedCount = await store.DeleteOldRequestsAsync(
    olderThan: DateTime.UtcNow.AddDays(-90),  // Keep 90 days of history
    cancellationToken: cancellationToken
);

Console.WriteLine($"Cleaned up {deletedCount} old approval requests");
```

## Future Enhancements

### Planned Features
1. **Notification System** - Email/Slack notifications for pending approvals
2. **Multi-Level Approval** - Escalation workflow for high-risk changes
3. **Approval Delegation** - Temporary delegation to backup approvers
4. **Approval Analytics** - Dashboards for approval metrics and trends
5. **Batch Approval** - Approve multiple related changes at once
6. **Comment Threading** - Discussion threads on approval requests
7. **Approval Templates** - Pre-configured approval policies for common scenarios

### Notification Integration (TODO)
```csharp
// Planned notification integration
private async Task SendNotificationAsync(
    ApprovalRequest request,
    ApprovalConfig config,
    CancellationToken cancellationToken)
{
    foreach (var channel in config.NotificationChannels)
    {
        switch (channel)
        {
            case "email":
                await SendEmailNotificationAsync(request);
                break;
            case "slack":
                await SendSlackNotificationAsync(request);
                break;
            case "webhook":
                await SendWebhookNotificationAsync(request);
                break;
        }
    }
}
```

## Troubleshooting

### Issue: Auto-Approval Not Working

**Symptom**: All proposals require manual approval even for low-risk changes

**Diagnosis**:
```csharp
// Check approval configuration
var config = await workflow.GetConfigAsync(promptId);
if (!config.AllowAutoApproval)
{
    Console.WriteLine("Auto-approval is disabled");
}

// Check auto-approval decision
var decision = await workflow.CheckAutoApprovalAsync(proposal);
Console.WriteLine($"Can auto-approve: {decision.CanAutoApprove}");
Console.WriteLine($"Risk factors: {string.Join(", ", decision.RiskFactors)}");
```

**Solutions**:
- Enable `AllowAutoApproval` in configuration
- Review risk thresholds (semantic change, performance)
- Ensure safety validation is passing
- Check proposal metadata for performance data

### Issue: Pending Requests Not Appearing

**Symptom**: Approver doesn't see pending requests

**Diagnosis**:
```csharp
// Check if requests exist
var allPending = await store.GetPendingRequestsAsync();
Console.WriteLine($"Total pending: {allPending.Count}");

// Check assignment
var myPending = await workflow.GetPendingRequestsAsync(myUserId);
Console.WriteLine($"Assigned to me: {myPending.Count}");
```

**Solutions**:
- Verify approver is in `ApproverUserIds` list
- Check `assigned_to` field on requests
- Ensure request status is `Pending`
- Verify database connection and indexes

## Related Components

- **Safety Coordinator** (`Safety/`) - Validates proposals before approval
- **Prompt Rewriter** (`Rewriter/`) - Generates proposals that need approval
- **Reflection Engine** (`Reflection/`) - Identifies improvement opportunities
- **Evaluation Pipeline** (`Evaluation/`) - Provides performance data for risk assessment

## Summary

The Approval Workflow provides:
- ✅ Intelligent auto-approval for low-risk changes
- ✅ Risk-based routing to appropriate approvers
- ✅ Flexible per-prompt configuration
- ✅ Complete audit trail
- ✅ Integration with safety and performance validation
- ✅ Production-ready PostgreSQL storage

This ensures human oversight remains effective while not becoming a bottleneck for safe, incremental improvements.
