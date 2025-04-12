using System.Diagnostics;
using COTA.Core.Models;

namespace COTA.Api.Services;

public class SolanaService
{
    private readonly IHeliusApi _heliusApi;
    private readonly IConfiguration _configuration;
    private readonly PriceService _priceService;
    private readonly Dictionary<string, (List<SolanaTransaction> Transactions, List<StakingReward> StakingRewards)> _transactionsByAddress = new();

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
            Console.WriteLine($"SolanaService: Fetching transactions for address {address}");

            if (_transactionsByAddress.ContainsKey(address))
            {
                Console.WriteLine($"SolanaService: Returning cached transactions for {address}");
                return _transactionsByAddress[address].Transactions;
            }

            var apiKey = _configuration["HeliusApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("SolanaService: Helius API key is missing.");
                return new List<SolanaTransaction>();
            }

            Console.WriteLine($"SolanaService: Calling Helius API for {address}");
            var heliusTransactions = await _heliusApi.GetTransactions(address, apiKey);

            if (!heliusTransactions.Any())
            {
                Console.WriteLine($"SolanaService: No transactions found for {address}");
                _transactionsByAddress[address] = (new List<SolanaTransaction>(), new List<StakingReward>());
                return new List<SolanaTransaction>();
            }

            var transactions = new List<SolanaTransaction>();
            var stakingRewards = new List<StakingReward>();

            foreach (var tx in heliusTransactions)
            {
                Console.WriteLine($"SolanaService: Processing tx {tx.Signature}, Timestamp: {tx.Timestamp}, Transfers: {tx.TokenTransfers?.Count ?? 0}");

                var timestamp = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp).UtcDateTime;

                if (tx.TokenTransfers != null && tx.TokenTransfers.Any())
                {
                    foreach (var transfer in tx.TokenTransfers)
                    {
                        if (transfer == null) continue;

                        Console.WriteLine($"SolanaService: Transfer - Mint: {transfer.Mint}, Amount: {transfer.Amount}, To: {transfer.ToUserAccount}, From: {transfer.FromUserAccount}");

                        // Determine transaction type
                        string txType;
                        if (string.IsNullOrEmpty(transfer.Mint) && transfer.ToUserAccount == address && transfer.Amount > 0)
                        {
                            txType = "StakeReward"; // Native SOL received (likely staking reward)
                        }
                        else if (transfer.ToUserAccount == address)
                        {
                            txType = "Buy"; // Token/SOL received
                        }
                        else if (transfer.FromUserAccount == address)
                        {
                            txType = "Sell"; // Token/SOL sent
                        }
                        else
                        {
                            continue;
                        }

                        var asset = string.IsNullOrEmpty(transfer.Mint) ? "SOL" : transfer.Mint;
                        var usdPrice = await _priceService.GetPriceAtTime("SOL", timestamp); // Hardcode SOL for now

                        var solTx = new SolanaTransaction
                        {
                            Signature = tx.Signature,
                            Timestamp = timestamp,
                            Amount = transfer.Amount,
                            Asset = asset,
                            Type = txType,
                            WalletAddress = address,
                            UsdValueAtTime = usdPrice * transfer.Amount
                        };
                        transactions.Add(solTx);

                        if (txType == "StakeReward")
                        {
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

            Console.WriteLine($"SolanaService: Fetched {transactions.Count} transactions, {stakingRewards.Count} staking rewards for {address}");
            _transactionsByAddress[address] = (transactions, stakingRewards);
            return transactions;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SolanaService: Error fetching transactions for {address}: {ex.Message}\n{ex.StackTrace}");
            return new List<SolanaTransaction>();
        }
    }

    public Task<List<StakingReward>> GetStakingRewards(string address)
    {
        Console.WriteLine($"SolanaService: Retrieving staking rewards for {address}");
        if (_transactionsByAddress.TryGetValue(address, out var data))
        {
            Console.WriteLine($"SolanaService: Returning {data.StakingRewards.Count} cached staking rewards for {address}");
            return Task.FromResult(data.StakingRewards);
        }
        Console.WriteLine($"SolanaService: No staking rewards found for {address}");
        return Task.FromResult(new List<StakingReward>());
    }
}