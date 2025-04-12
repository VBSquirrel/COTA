namespace COTA.Core.Models;

public class SolanaTransaction
{
    public string? Signature { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Amount { get; set; }
    public string? Asset { get; set; }
    public string? Type { get; set; }
    public string? WalletAddress { get; set; }
    public decimal? UsdValueAtTime { get; set; }
}