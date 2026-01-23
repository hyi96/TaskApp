using System;
using System.Collections.Generic;

namespace TaskApp.Data;

public class RewardData
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsClaimed { get; set; }
    public bool IsRepeatable { get; set; }
    public int ClaimCount { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public double GoldCost { get; set; }
    public System.Collections.Generic.List<string> Tags { get; set; } = new();
    public List<Guid> LinkedTaskIds { get; set; } = new();
}
