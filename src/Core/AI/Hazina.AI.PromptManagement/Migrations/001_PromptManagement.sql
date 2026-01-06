-- Hazina Prompt Management System - Database Schema
-- Version: 1.0
-- Date: 2026-01-06
-- Description: Self-learning AI prompt versioning and management

-- ============================================================================
-- PROMPT TEMPLATES
-- ============================================================================

-- Main prompt template registry
CREATE TABLE IF NOT EXISTS prompt_templates (
    prompt_id VARCHAR(255) PRIMARY KEY,
    current_version VARCHAR(64) NOT NULL,  -- SHA-256 hash of current version
    name VARCHAR(255) NOT NULL,
    description TEXT,
    template_engine VARCHAR(50) NOT NULL DEFAULT 'Handlebars',  -- "Handlebars" | "Liquid" | "Scriban"
    category VARCHAR(100),  -- "agent" | "rag" | "cognitive_channel" | "policy"
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    metadata JSONB  -- Flexible metadata storage
);

CREATE INDEX idx_prompt_templates_category ON prompt_templates(category);
CREATE INDEX idx_prompt_templates_updated_at ON prompt_templates(updated_at DESC);

-- ============================================================================
-- PROMPT VERSIONS
-- ============================================================================

-- Version history (append-only log)
CREATE TABLE IF NOT EXISTS prompt_versions (
    version_id VARCHAR(64) PRIMARY KEY,  -- SHA-256 of (prompt_id + content + timestamp)
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    version_number INT NOT NULL,  -- Sequential version number per prompt
    template TEXT NOT NULL,  -- The actual template content
    variables JSONB,  -- Template variable definitions
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255) NOT NULL,  -- "human:user@example.com" | "system:rewriter" | "system:migration"
    reason TEXT,  -- Why was this version created?
    parent_version VARCHAR(64),  -- For tracking lineage (Git-like)
    status VARCHAR(50) NOT NULL DEFAULT 'active',  -- "active" | "archived" | "rolled_back" | "sandbox"
    metadata JSONB,  -- Additional version-specific data
    UNIQUE(prompt_id, version_number)
);

CREATE INDEX idx_prompt_versions_prompt_id ON prompt_versions(prompt_id);
CREATE INDEX idx_prompt_versions_created_at ON prompt_versions(created_at DESC);
CREATE INDEX idx_prompt_versions_status ON prompt_versions(status);
CREATE INDEX idx_prompt_versions_parent ON prompt_versions(parent_version);

-- ============================================================================
-- PROMPT METRICS
-- ============================================================================

-- Aggregated performance metrics per version
CREATE TABLE IF NOT EXISTS prompt_metrics (
    metric_id SERIAL PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    version_id VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id) ON DELETE CASCADE,
    timestamp TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    total_uses INT NOT NULL DEFAULT 0,
    success_count INT NOT NULL DEFAULT 0,
    failure_count INT NOT NULL DEFAULT 0,
    avg_user_rating FLOAT,  -- 0.0 to 1.0
    avg_confidence FLOAT,  -- 0.0 to 1.0
    avg_latency_ms FLOAT,
    total_tokens_used BIGINT DEFAULT 0,
    total_cost_usd DECIMAL(10, 6) DEFAULT 0,
    eval_metrics JSONB,  -- { "mrr": 0.75, "ndcg": 0.82, "precision@5": 0.9, ... }
    UNIQUE(prompt_id, version_id, DATE(timestamp))
);

CREATE INDEX idx_prompt_metrics_version ON prompt_metrics(version_id);
CREATE INDEX idx_prompt_metrics_timestamp ON prompt_metrics(timestamp DESC);
CREATE INDEX idx_prompt_metrics_prompt_timestamp ON prompt_metrics(prompt_id, timestamp DESC);

-- ============================================================================
-- EVALUATION TEST SETS
-- ============================================================================

-- Ground truth test sets for evaluation
CREATE TABLE IF NOT EXISTS eval_test_sets (
    test_set_id VARCHAR(255) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    cases JSONB NOT NULL,  -- Array of { query, expectedOutput?, relevantDocuments?, context? }
    rubrics JSONB,  -- Custom quality rubrics: { "accuracy": {...}, "relevance": {...} }
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    category VARCHAR(100),  -- Same as prompt category for matching
    metadata JSONB
);

CREATE INDEX idx_eval_test_sets_category ON eval_test_sets(category);
CREATE INDEX idx_eval_test_sets_updated_at ON eval_test_sets(updated_at DESC);

-- ============================================================================
-- EVALUATION RUNS
-- ============================================================================

-- Evaluation run history
CREATE TABLE IF NOT EXISTS eval_runs (
    run_id VARCHAR(255) PRIMARY KEY,
    test_set_id VARCHAR(255) NOT NULL REFERENCES eval_test_sets(test_set_id) ON DELETE CASCADE,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    version_id VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id) ON DELETE CASCADE,
    started_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    status VARCHAR(50) NOT NULL DEFAULT 'running',  -- "running" | "completed" | "failed" | "cancelled"
    metrics JSONB,  -- Aggregate metrics: { "mrr": 0.75, "ndcg": 0.82, ... }
    case_results JSONB,  -- Per-case results: [{ caseId, query, response, score, ... }]
    errors TEXT,
    metadata JSONB
);

CREATE INDEX idx_eval_runs_prompt_version ON eval_runs(prompt_id, version_id);
CREATE INDEX idx_eval_runs_started_at ON eval_runs(started_at DESC);
CREATE INDEX idx_eval_runs_status ON eval_runs(status);
CREATE INDEX idx_eval_runs_test_set ON eval_runs(test_set_id);

-- ============================================================================
-- REFLECTION REPORTS
-- ============================================================================

-- Automated reflection and pattern analysis
CREATE TABLE IF NOT EXISTS reflection_reports (
    report_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    start_date TIMESTAMP NOT NULL,
    end_date TIMESTAMP NOT NULL,
    total_runs INT NOT NULL,
    success_rate FLOAT NOT NULL,  -- 0.0 to 1.0
    failure_patterns JSONB,  -- Array of { pattern, frequency, examples[], rootCause }
    success_patterns JSONB,  -- Array of { pattern, frequency, examples[], keyFactors[] }
    improvement_hypotheses JSONB,  -- Array of { hypothesis, expectedImpact, rationale, priority }
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    metadata JSONB
);

CREATE INDEX idx_reflection_reports_prompt_id ON reflection_reports(prompt_id);
CREATE INDEX idx_reflection_reports_created_at ON reflection_reports(created_at DESC);
CREATE INDEX idx_reflection_reports_date_range ON reflection_reports(start_date, end_date);

-- ============================================================================
-- PROMPT PROPOSALS (Rewriter Output)
-- ============================================================================

-- Automated prompt improvement proposals
CREATE TABLE IF NOT EXISTS prompt_proposals (
    proposal_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    current_version VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id),
    proposed_template TEXT NOT NULL,
    proposed_version VARCHAR(64),  -- Hash of proposed content (becomes version_id if approved)
    changes JSONB NOT NULL,  -- Array of { type: "Add"|"Remove"|"Modify", section, oldValue, newValue, reason }
    rationale TEXT NOT NULL,
    expected_improvement FLOAT,  -- Expected % improvement
    status VARCHAR(50) NOT NULL DEFAULT 'pending',  -- "pending" | "approved" | "rejected" | "deployed" | "rolled_back"
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255) NOT NULL DEFAULT 'system:rewriter',
    reflection_report_id VARCHAR(255) REFERENCES reflection_reports(report_id),
    metadata JSONB
);

CREATE INDEX idx_prompt_proposals_prompt_id ON prompt_proposals(prompt_id);
CREATE INDEX idx_prompt_proposals_status ON prompt_proposals(status);
CREATE INDEX idx_prompt_proposals_created_at ON prompt_proposals(created_at DESC);

-- ============================================================================
-- APPROVAL WORKFLOW
-- ============================================================================

-- Human approval/rejection actions
CREATE TABLE IF NOT EXISTS approval_actions (
    action_id SERIAL PRIMARY KEY,
    proposal_id VARCHAR(255) NOT NULL REFERENCES prompt_proposals(proposal_id) ON DELETE CASCADE,
    action VARCHAR(50) NOT NULL,  -- "approve" | "reject" | "request_changes"
    approver VARCHAR(255) NOT NULL,  -- "user@example.com"
    comments TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_approval_actions_proposal_id ON approval_actions(proposal_id);
CREATE INDEX idx_approval_actions_approver ON approval_actions(approver);
CREATE INDEX idx_approval_actions_created_at ON approval_actions(created_at DESC);

-- ============================================================================
-- SAFETY CHECKS
-- ============================================================================

-- Safety validation results
CREATE TABLE IF NOT EXISTS safety_checks (
    check_id SERIAL PRIMARY KEY,
    proposal_id VARCHAR(255) NOT NULL REFERENCES prompt_proposals(proposal_id) ON DELETE CASCADE,
    check_type VARCHAR(100) NOT NULL,  -- "cooldown" | "threshold" | "drift" | "sandbox"
    passed BOOLEAN NOT NULL,
    violations JSONB,  -- Array of { type, message, severity: 0.0-1.0 }
    details JSONB,  -- Check-specific details
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_safety_checks_proposal_id ON safety_checks(proposal_id);
CREATE INDEX idx_safety_checks_type ON safety_checks(check_type);
CREATE INDEX idx_safety_checks_passed ON safety_checks(passed);

-- ============================================================================
-- SANDBOX TESTS
-- ============================================================================

-- Sandbox testing results before production
CREATE TABLE IF NOT EXISTS sandbox_tests (
    test_id VARCHAR(255) PRIMARY KEY,
    proposal_id VARCHAR(255) NOT NULL REFERENCES prompt_proposals(proposal_id) ON DELETE CASCADE,
    test_set_id VARCHAR(255) NOT NULL REFERENCES eval_test_sets(test_set_id) ON DELETE CASCADE,
    started_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP,
    baseline_metrics JSONB,  -- Current version performance
    proposed_metrics JSONB,  -- New version performance
    improvement_pct FLOAT,  -- (proposed - baseline) / baseline * 100
    passed BOOLEAN,  -- Did it meet safety thresholds?
    errors TEXT,
    metadata JSONB
);

CREATE INDEX idx_sandbox_tests_proposal_id ON sandbox_tests(proposal_id);
CREATE INDEX idx_sandbox_tests_passed ON sandbox_tests(passed);
CREATE INDEX idx_sandbox_tests_completed_at ON sandbox_tests(completed_at DESC);

-- ============================================================================
-- ROLLBACK HISTORY
-- ============================================================================

-- Track all rollback operations
CREATE TABLE IF NOT EXISTS rollback_history (
    rollback_id SERIAL PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    from_version VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id),
    to_version VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id),
    reason TEXT NOT NULL,
    initiated_by VARCHAR(255) NOT NULL,  -- "user@example.com" | "system:auto_rollback"
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_rollback_history_prompt_id ON rollback_history(prompt_id);
CREATE INDEX idx_rollback_history_created_at ON rollback_history(created_at DESC);

-- ============================================================================
-- FUNCTIONS AND TRIGGERS
-- ============================================================================

-- Function to update the updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Trigger for prompt_templates
DROP TRIGGER IF EXISTS update_prompt_templates_updated_at ON prompt_templates;
CREATE TRIGGER update_prompt_templates_updated_at
    BEFORE UPDATE ON prompt_templates
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Trigger for eval_test_sets
DROP TRIGGER IF EXISTS update_eval_test_sets_updated_at ON eval_test_sets;
CREATE TRIGGER update_eval_test_sets_updated_at
    BEFORE UPDATE ON eval_test_sets
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================================================
-- VIEWS FOR COMMON QUERIES
-- ============================================================================

-- View: Latest version for each prompt
CREATE OR REPLACE VIEW latest_prompt_versions AS
SELECT
    pt.prompt_id,
    pt.name,
    pt.current_version,
    pv.version_number,
    pv.template,
    pv.created_at,
    pv.created_by,
    pv.status
FROM prompt_templates pt
JOIN prompt_versions pv ON pt.current_version = pv.version_id;

-- View: Prompt performance summary
CREATE OR REPLACE VIEW prompt_performance_summary AS
SELECT
    pt.prompt_id,
    pt.name,
    pt.current_version,
    pv.version_number,
    COALESCE(SUM(pm.total_uses), 0) as total_uses,
    COALESCE(SUM(pm.success_count), 0) as success_count,
    COALESCE(SUM(pm.failure_count), 0) as failure_count,
    CASE
        WHEN SUM(pm.total_uses) > 0
        THEN ROUND((SUM(pm.success_count)::DECIMAL / SUM(pm.total_uses) * 100), 2)
        ELSE 0
    END as success_rate_pct,
    ROUND(AVG(pm.avg_user_rating)::DECIMAL, 3) as avg_rating,
    ROUND(AVG(pm.avg_confidence)::DECIMAL, 3) as avg_confidence,
    ROUND(AVG(pm.avg_latency_ms)::DECIMAL, 2) as avg_latency_ms,
    SUM(pm.total_tokens_used) as total_tokens,
    SUM(pm.total_cost_usd) as total_cost_usd
FROM prompt_templates pt
JOIN prompt_versions pv ON pt.current_version = pv.version_id
LEFT JOIN prompt_metrics pm ON pv.version_id = pm.version_id
GROUP BY pt.prompt_id, pt.name, pt.current_version, pv.version_number;

-- View: Pending proposals with safety check status
CREATE OR REPLACE VIEW pending_proposals_with_checks AS
SELECT
    pp.proposal_id,
    pp.prompt_id,
    pt.name as prompt_name,
    pp.rationale,
    pp.expected_improvement,
    pp.created_at,
    COUNT(DISTINCT sc.check_id) as total_checks,
    SUM(CASE WHEN sc.passed THEN 1 ELSE 0 END) as passed_checks,
    BOOL_AND(sc.passed) as all_checks_passed
FROM prompt_proposals pp
JOIN prompt_templates pt ON pp.prompt_id = pt.prompt_id
LEFT JOIN safety_checks sc ON pp.proposal_id = sc.proposal_id
WHERE pp.status = 'pending'
GROUP BY pp.proposal_id, pp.prompt_id, pt.name, pp.rationale, pp.expected_improvement, pp.created_at;

-- ============================================================================
-- INITIAL SEED DATA (Optional)
-- ============================================================================

-- Example: Insert a default test set
-- INSERT INTO eval_test_sets (test_set_id, name, description, cases, category)
-- VALUES (
--     'default-rag-test',
--     'Default RAG Test Set',
--     'Basic test cases for RAG evaluation',
--     '[
--         {"query": "What is Hazina?", "relevantDocuments": ["hazina-overview.md"]},
--         {"query": "How do I install?", "relevantDocuments": ["installation.md"]}
--     ]'::jsonb,
--     'rag'
-- );

-- ============================================================================
-- COMMENTS
-- ============================================================================

COMMENT ON TABLE prompt_templates IS 'Registry of all prompt templates in the system';
COMMENT ON TABLE prompt_versions IS 'Version history for all prompts (append-only log)';
COMMENT ON TABLE prompt_metrics IS 'Aggregated performance metrics per prompt version';
COMMENT ON TABLE eval_test_sets IS 'Ground truth test sets for automated evaluation';
COMMENT ON TABLE eval_runs IS 'History of evaluation runs with results';
COMMENT ON TABLE reflection_reports IS 'Automated reflection analysis and improvement suggestions';
COMMENT ON TABLE prompt_proposals IS 'Automated prompt improvement proposals from rewriter';
COMMENT ON TABLE approval_actions IS 'Human approval/rejection history for proposals';
COMMENT ON TABLE safety_checks IS 'Safety validation results for proposals';
COMMENT ON TABLE sandbox_tests IS 'Sandbox testing results before production deployment';
COMMENT ON TABLE rollback_history IS 'History of all rollback operations';

-- End of migration

-- ============================================================================
-- EVALUATION SCHEDULES
-- ============================================================================

-- Scheduled evaluation runs
CREATE TABLE IF NOT EXISTS evaluation_schedules (
    schedule_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    test_set_id VARCHAR(255) NOT NULL REFERENCES eval_test_sets(test_set_id) ON DELETE CASCADE,
    cron_expression VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_run_at TIMESTAMP,
    next_run_at TIMESTAMP,
    last_run_id VARCHAR(255)
);

CREATE INDEX idx_evaluation_schedules_prompt_id ON evaluation_schedules(prompt_id);
CREATE INDEX idx_evaluation_schedules_is_active ON evaluation_schedules(is_active);
CREATE INDEX idx_evaluation_schedules_next_run_at ON evaluation_schedules(next_run_at);

-- ============================================================================
-- REGRESSION REPORTS
-- ============================================================================

-- Regression detection reports
CREATE TABLE IF NOT EXISTS regression_reports (
    report_id VARCHAR(255) PRIMARY KEY,
    prompt_id VARCHAR(255) NOT NULL REFERENCES prompt_templates(prompt_id) ON DELETE CASCADE,
    baseline_version_id VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id),
    new_version_id VARCHAR(64) NOT NULL REFERENCES prompt_versions(version_id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    baseline_metrics JSONB NOT NULL,
    new_metrics JSONB NOT NULL,
    changes JSONB NOT NULL,  -- Percent changes per metric
    has_regression BOOLEAN NOT NULL,
    issues JSONB,  -- Array of regression issues
    p_values JSONB,  -- Statistical significance p-values
    is_significant JSONB  -- Statistical significance flags per metric
);

CREATE INDEX idx_regression_reports_prompt_id ON regression_reports(prompt_id);
CREATE INDEX idx_regression_reports_created_at ON regression_reports(created_at DESC);
CREATE INDEX idx_regression_reports_has_regression ON regression_reports(has_regression);

COMMENT ON TABLE evaluation_schedules IS 'Scheduled recurring evaluation runs';
COMMENT ON TABLE regression_reports IS 'Regression analysis comparing prompt versions';

