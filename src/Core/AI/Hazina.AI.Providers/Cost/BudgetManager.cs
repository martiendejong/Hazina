namespace Hazina.AI.Providers.Cost;

/// <summary>
/// Manages budgets and alerts for LLM costs
/// </summary>
public class BudgetManager
{
    private readonly ICostTracker _costTracker;
    private readonly Dictionary<string, Budget> _budgets = new();
    private readonly List<BudgetAlert> _alerts = new();
    private readonly object _lock = new();

    public event EventHandler<BudgetAlertEventArgs>? BudgetAlertTriggered;

    public BudgetManager(ICostTracker costTracker)
    {
        _costTracker = costTracker ?? throw new ArgumentNullException(nameof(costTracker));
    }

    /// <summary>
    /// Set budget for a provider
    /// </summary>
    public void SetBudget(string providerName, decimal limit, BudgetPeriod period)
    {
        lock (_lock)
        {
            _budgets[providerName] = new Budget
            {
                ProviderName = providerName,
                Limit = limit,
                Period = period,
                StartDate = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Set global budget across all providers
    /// </summary>
    public void SetGlobalBudget(decimal limit, BudgetPeriod period)
    {
        SetBudget("__global__", limit, period);
    }

    /// <summary>
    /// Get budget for a provider
    /// </summary>
    public Budget? GetBudget(string providerName)
    {
        lock (_lock)
        {
            return _budgets.TryGetValue(providerName, out var budget) ? budget : null;
        }
    }

    /// <summary>
    /// Check if budget exceeded
    /// </summary>
    public bool IsBudgetExceeded(string providerName)
    {
        lock (_lock)
        {
            if (!_budgets.TryGetValue(providerName, out var budget))
                return false;

            var cost = providerName == "__global__"
                ? _costTracker.GetTotalCost()
                : _costTracker.GetTotalCost(providerName);

            return cost >= budget.Limit;
        }
    }

    /// <summary>
    /// Get budget utilization percentage (0-100+)
    /// </summary>
    public double GetBudgetUtilization(string providerName)
    {
        lock (_lock)
        {
            if (!_budgets.TryGetValue(providerName, out var budget))
                return 0.0;

            var cost = providerName == "__global__"
                ? _costTracker.GetTotalCost()
                : _costTracker.GetTotalCost(providerName);

            return (double)(cost / budget.Limit) * 100.0;
        }
    }

    /// <summary>
    /// Add budget alert threshold
    /// </summary>
    public void AddAlert(string providerName, double thresholdPercentage, string? message = null)
    {
        lock (_lock)
        {
            _alerts.Add(new BudgetAlert
            {
                ProviderName = providerName,
                ThresholdPercentage = thresholdPercentage,
                Message = message ?? $"Budget threshold {thresholdPercentage}% reached for {providerName}",
                IsTriggered = false
            });
        }
    }

    /// <summary>
    /// Check for budget alerts
    /// </summary>
    public void CheckAlerts()
    {
        lock (_lock)
        {
            foreach (var alert in _alerts.Where(a => !a.IsTriggered))
            {
                var utilization = GetBudgetUtilization(alert.ProviderName);
                if (utilization >= alert.ThresholdPercentage)
                {
                    alert.IsTriggered = true;
                    alert.TriggeredAt = DateTime.UtcNow;

                    BudgetAlertTriggered?.Invoke(this, new BudgetAlertEventArgs
                    {
                        Alert = alert,
                        CurrentUtilization = utilization,
                        CurrentCost = alert.ProviderName == "__global__"
                            ? _costTracker.GetTotalCost()
                            : _costTracker.GetTotalCost(alert.ProviderName)
                    });
                }
            }
        }
    }

    /// <summary>
    /// Reset alerts
    /// </summary>
    public void ResetAlerts()
    {
        lock (_lock)
        {
            foreach (var alert in _alerts)
            {
                alert.IsTriggered = false;
                alert.TriggeredAt = null;
            }
        }
    }

    /// <summary>
    /// Get all triggered alerts
    /// </summary>
    public IEnumerable<BudgetAlert> GetTriggeredAlerts()
    {
        lock (_lock)
        {
            return _alerts.Where(a => a.IsTriggered).ToList();
        }
    }
}

/// <summary>
/// Budget configuration
/// </summary>
public class Budget
{
    public string ProviderName { get; set; } = string.Empty;
    public decimal Limit { get; set; }
    public BudgetPeriod Period { get; set; }
    public DateTime StartDate { get; set; }
}

/// <summary>
/// Budget period
/// </summary>
public enum BudgetPeriod
{
    Daily,
    Weekly,
    Monthly,
    Total
}

/// <summary>
/// Budget alert configuration
/// </summary>
public class BudgetAlert
{
    public string ProviderName { get; set; } = string.Empty;
    public double ThresholdPercentage { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
}

/// <summary>
/// Budget alert event args
/// </summary>
public class BudgetAlertEventArgs : EventArgs
{
    public required BudgetAlert Alert { get; init; }
    public double CurrentUtilization { get; init; }
    public decimal CurrentCost { get; init; }
}
