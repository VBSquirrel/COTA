namespace COTA.Api.Services;

public class HeliusTransaction
{
    public string? Signature { get; set; }
    public long Timestamp { get; set; }
    public List<Transfer>? TokenTransfers { get; set; }
    // Add fields if Helius provides type/description
    public string? Type { get; set; }
    public string? Description { get; set; }
}

public class Transfer
{
    public string? Mint { get; set; }
    public decimal Amount { get; set; }
    public string? FromUserAccount { get; set; }
    public string? ToUserAccount { get; set; }
}