namespace Hazina.App.HazinaCoder.Core.Monitoring;

/// <summary>
/// Tracks API costs and enforces budgets
/// Iteration 13: Cost management
/// </summary>
public class CostTracker
{
    public class CostEntry
    {
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
        public decimal Cost { get; set; }
        public string Operation { get; set; } = "";
    }

    private readonly List<CostEntry> _entries = new();
    private readonly decimal _dailyBudget;
    private readonly decimal _monthlyBudget;

    public CostTracker(decimal dailyBudget = 10.0m, decimal monthlyBudget = 300.0m)
    {
        _dailyBudget = dailyBudget;
        _monthlyBudget = monthlyBudget;
    }

    public void RecordCost(string provider, string model, long inputTokens, long outputTokens, decimal cost, string operation = "")
    {
        _entries.Add(new CostEntry
        {
            Timestamp = DateTime.UtcNow,
            Provider = provider,
            Model = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Cost = cost,
            Operation = operation
        });
    }

    public decimal GetDailyCost()
    {
        var today = DateTime.UtcNow.Date;
        return _entries
            .Where(e => e.Timestamp.Date == today)
            .Sum(e => e.Cost);
    }

    public decimal GetMonthlyCost()
    {
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        return _entries
            .Where(e => e.Timestamp >= thisMonth)
            .Sum(e => e.Cost);
    }

    public bool IsWithinDailyBudget() => GetDailyCost() < _dailyBudget;
    public bool IsWithinMonthlyBudget() => GetMonthlyCost() < _monthlyBudget;

    public decimal GetRemainingDailyBudget() => Math.Max(0, _dailyBudget - GetDailyCost());
    public decimal GetRemainingMonthlyBudget() => Math.Max(0, _monthlyBudget - GetMonthlyCost());

    public Dictionary<string, decimal> GetCostByProvider()
    {
        return _entries
            .GroupBy(e => e.Provider)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost));
    }

    public Dictionary<string, decimal> GetCostByModel()
    {
        return _entries
            .GroupBy(e => e.Model)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Cost));
    }

    public string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== Cost Report ===");
        sb.AppendLine($"Daily Cost: ${GetDailyCost():F2} / ${_dailyBudget:F2} ({GetDailyCost() / _dailyBudget * 100:F1}%)");
        sb.AppendLine($"Monthly Cost: ${GetMonthlyCost():F2} / ${_monthlyBudget:F2} ({GetMonthlyCost() / _monthlyBudget * 100:F1}%)");
        sb.AppendLine();

        sb.AppendLine("By Provider:");
        foreach (var (provider, cost) in GetCostByProvider())
        {
            sb.AppendLine($"  {provider}: ${cost:F2}");
        }

        sb.AppendLine();
        sb.AppendLine("By Model:");
        foreach (var (model, cost) in GetCostByModel())
        {
            sb.AppendLine($"  {model}: ${cost:F2}");
        }

        return sb.ToString();
    }

    public void ExportToCsv(string path)
    {
        var lines = new List<string> { "Timestamp,Provider,Model,InputTokens,OutputTokens,Cost,Operation" };

        lines.AddRange(_entries.Select(e =>
            $"{e.Timestamp:O},{e.Provider},{e.Model},{e.InputTokens},{e.OutputTokens},{e.Cost},{e.Operation}"));

        File.WriteAllLines(path, lines);
    }
}
