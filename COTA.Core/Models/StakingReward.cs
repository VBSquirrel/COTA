namespace COTA.Core.Models;

public class StakingReward
{
    public string? WalletAddress { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal UsdValueAtTime { get; set; }
}