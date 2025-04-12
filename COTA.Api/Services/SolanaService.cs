using Refit;
using COTA.Core.Models;

namespace COTA.Api.Services;

public interface IHeliusApi
{
    [Get("/v0/addresses/{address}/transactions?api-key={apiKey}")]
    Task<List<HeliusTransaction>> GetTransactions(string address, string apiKey);

    [Get("/v0/addresses/{address}/staking-rewards?api-key={apiKey}")]
    Task<List<HeliusStakingReward>> GetStakingRewards(string address, string apiKey);
}

public class HeliusTransaction
{
    public string? Signature { get; set; }
    public long Timestamp { get; set; }
    public List<Transfer>? TokenTransfers { get; set; }
}

public class Transfer
{
    public string? Mint { get; set; }
    public decimal Amount { get; set; }
    public string? FromUserAccount { get; set; }
    public string? ToUserAccount { get; set; }
}

public class HeliusStakingReward
{
    public decimal Amount { get; set; }
    public long Timestamp { get; set; }
}

public class SolanaService
{
    private readonly IHeliusApi _heliusApi;
    private readonly PriceService _priceService;
    private readonly string _apiKey;

    public SolanaService(IHeliusApi heliusApi, PriceService priceService, IConfiguration configuration)
    {
        _heliusApi = heliusApi;
        _priceService = priceService;
        _apiKey = configuration["HeliusApiKey"] ?? throw new InvalidOperationException("Helius API key missing.");
    }

    public async Task<List<SolanaTransaction>> GetTransactions(string address)
    {
        var response = await _heliusApi.GetTransactions(address, _apiKey);
        var transactions = new List<SolanaTransaction>();

        foreach (var tx in response ?? new List<HeliusTransaction>())
        {
            foreach (var transfer in tx.TokenTransfers ?? new List<Transfer>())
            {
                var isSell = transfer.FromUserAccount == address;
                var asset = transfer.Mint == "So11111111111111111111111111111111111111112" ? "SOL" : GetTokenSymbol(transfer.Mint);
                var timestamp = UnixTimeStampToDateTime(tx.Timestamp);
                var amount = transfer.Amount / 1_000_000_000m;

                transactions.Add(new SolanaTransaction
                {
                    Signature = tx.Signature,
                    WalletAddress = address,
                    Amount = amount,
                    Asset = asset,
                    Timestamp = timestamp,
                    Type = isSell ? "Sell" : "Buy",
                    UsdValueAtTime = amount * await _priceService.GetPriceAtTime(asset, timestamp)
                });
            }
        }

        var rewards = await GetStakingRewards(address);
        transactions.AddRange(rewards.Select(r => new SolanaTransaction
        {
            Signature = "reward_" + r.Timestamp.Ticks,
            WalletAddress = address,
            Amount = r.Amount,
            Asset = "SOL",
            Timestamp = r.Timestamp,
            Type = "StakeReward",
            UsdValueAtTime = r.UsdValueAtTime
        }));

        return transactions.OrderBy(t => t.Timestamp).ToList();
    }

    public async Task<List<StakingReward>> GetStakingRewards(string address)
    {
        var response = await _heliusApi.GetStakingRewards(address, _apiKey);
        var rewards = new List<StakingReward>();

        foreach (var reward in response ?? new List<HeliusStakingReward>())
        {
            var timestamp = UnixTimeStampToDateTime(reward.Timestamp);
            var amount = reward.Amount / 1_000_000_000m;
            rewards.Add(new StakingReward
            {
                WalletAddress = address,
                Amount = amount,
                Timestamp = timestamp,
                UsdValueAtTime = amount * await _priceService.GetPriceAtTime("SOL", timestamp)
            });
        }

        return rewards;
    }

    private string GetTokenSymbol(string? mint)
    {
        return mint != null && mint.StartsWith("EPjFW") ? "USDC" : "Unknown";
    }

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        return DateTime.UnixEpoch.AddSeconds(unixTimeStamp);
    }
}