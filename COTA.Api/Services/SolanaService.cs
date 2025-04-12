using System.Diagnostics;
using COTA.Core.Models;

namespace COTA.Api.Services;

public class SolanaService
{
    private readonly IHeliusApi _heliusApi;
    private readonly IConfiguration _configuration;
    private readonly PriceService _priceService;
    private readonly Dictionary<string, (List<SolanaTransaction> Transactions, List<StakingReward> StakingRewards)> _transactionsByAddress = [];

    public SolanaService(IHeliusApi heliusApi, IConfiguration configuration, PriceService priceService)
    {
        _heliusApi = heliusApi;
        _configuration = configuration;
        _priceService = priceService;
    }

    public async Task<List<SolanaTransaction>> GetTransactions(string address)
    {
        try
        {
            if (_transactionsByAddress.ContainsKey(address))
            {
                Debug.WriteLine($"SolanaService: Returning cached transactions for {address}");
                return _transactionsByAddress[address].Transactions;
            }

            var apiKey = _configuration["HeliusApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.WriteLine("SolanaService: Helius API key is missing.");
                return new List<SolanaTransaction>();
            }
            var heliusTransactions = await _heliusApi.GetTransactions(address, apiKey);

            if (!heliusTransactions.Any())
            {
                Debug.WriteLine($"SolanaService: No transactions found for {address}");
                _transactionsByAddress[address] = (new List<SolanaTransaction>(), new List<StakingReward>());
                return new List<SolanaTransaction>();
            }

            var transactions = new List<SolanaTransaction>();
            var stakingRewards = new List<StakingReward>();

            foreach (var tx in heliusTransactions)
            {
                // Log transaction details for debugging
                Debug.WriteLine($"SolanaService: Processing tx {tx.Signature}, Timestamp: {tx.Timestamp}, Transfers: {tx.TokenTransfers?.Count ?? 0}");

                // Map to SolanaTransaction
                var solTx = new SolanaTransaction
                {
                    Signature = tx.Signature,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp).UtcDateTime,
                    // Amount from transfers or fallback
                    Amount = tx.TokenTransfers?.Sum(t => t.Amount) ?? 0,
                    Asset = tx.TokenTransfers?.FirstOrDefault()?.Mint ?? "SOL",
                    Type = "TRANSFER" // Placeholder, adjust if Type is added
                };
                transactions.Add(solTx);

                // Extract staking rewards
                if (tx.TokenTransfers != null && tx.TokenTransfers.Any())
                {
                    foreach (var transfer in tx.TokenTransfers)
                    {
                        // Assume staking rewards are SOL transfers to the address
                        // Adjust based on actual Helius data (e.g., specific Mint or FromUserAccount)
                        if (transfer.ToUserAccount == address && transfer.Mint == null) // Native SOL transfer
                        {
                            var timestamp = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp).UtcDateTime;
                            var usdPrice = await _priceService.GetPriceAtTime("SOL", timestamp);
                            stakingRewards.Add(new StakingReward
                            {
                                WalletAddress = address,
                                Amount = transfer.Amount,
                                Timestamp = timestamp,
                                UsdValueAtTime = usdPrice * transfer.Amount
                            });
                        }
                    }
                }
            }

            Debug.WriteLine($"SolanaService: Fetched {transactions.Count} transactions, {stakingRewards.Count} staking rewards for {address}");
            _transactionsByAddress[address] = (transactions, stakingRewards);
            return transactions;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SolanaService: Error fetching transactions for {address}: {ex.Message}");
            return new List<SolanaTransaction>();
        }
    }

    public Task<List<StakingReward>> GetStakingRewards(string address)
    {
        if (_transactionsByAddress.TryGetValue(address, out var data))
        {
            Debug.WriteLine($"SolanaService: Returning {data.StakingRewards.Count} cached staking rewards for {address}");
            return Task.FromResult(data.StakingRewards);
        }
        Debug.WriteLine($"SolanaService: No staking rewards found for {address}");
        return Task.FromResult(new List<StakingReward>());
    }
}