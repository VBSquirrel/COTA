using COTA.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace COTA.Api.Services
{
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

        public async Task<List<SolanaTransaction>> GetTransactions(string address, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                Console.WriteLine($"SolanaService: Fetching transactions for address {address} with date range Start: {startDate?.ToString() ?? "null"}, End: {endDate?.ToString() ?? "null"}");

                // Check cache first
                if (_transactionsByAddress.ContainsKey(address))
                {
                    Console.WriteLine($"SolanaService: Returning cached transactions for {address}");
                    var cachedTransactions = _transactionsByAddress[address].Transactions;

                    // Filter cached transactions by date range if provided
                    if (startDate.HasValue || endDate.HasValue)
                    {
                        var filteredTransactions = cachedTransactions.Where(tx =>
                            (!startDate.HasValue || tx.Timestamp >= startDate.Value) &&
                            (!endDate.HasValue || tx.Timestamp <= endDate.Value)
                        ).ToList();
                        Console.WriteLine($"SolanaService: Filtered cached transactions to {filteredTransactions.Count} within date range");
                        return filteredTransactions;
                    }

                    return cachedTransactions;
                }

                var apiKey = _configuration["HeliusApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("SolanaService: Helius API key is missing.");
                    return new List<SolanaTransaction>();
                }

                Console.WriteLine($"SolanaService: Calling Helius API for {address}");

                // Convert dates to Unix timestamps for Helius API
                long? untilTimestamp = startDate.HasValue ? ((DateTimeOffset)startDate.Value).ToUnixTimeSeconds() : null;
                long? beforeTimestamp = endDate.HasValue ? ((DateTimeOffset)endDate.Value).ToUnixTimeSeconds() : null;

                var heliusTransactionsResponse = await _heliusApi.GetTransactions(address, apiKey, untilTimestamp, beforeTimestamp);

                if (heliusTransactionsResponse == null)
                {
                    Console.WriteLine($"SolanaService: Helius API returned null for {address}");
                    _transactionsByAddress[address] = (new List<SolanaTransaction>(), new List<StakingReward>());
                    return new List<SolanaTransaction>();
                }

                var heliusTransactions = heliusTransactionsResponse.ToList();
                Console.WriteLine($"SolanaService: Received {heliusTransactions.Count} transactions");
                Console.WriteLine($"SolanaService: Raw transactions: {JsonConvert.SerializeObject(heliusTransactions, Formatting.Indented)}");

                var transactions = new List<SolanaTransaction>();
                var stakingRewards = new List<StakingReward>();

                foreach (var tx in heliusTransactions)
                {
                    if (tx == null)
                    {
                        Console.WriteLine($"SolanaService: Skipping null transaction for {address}");
                        continue;
                    }

                    Console.WriteLine($"SolanaService: Processing tx {tx.Signature}, Timestamp: {tx.Timestamp}, Transfers: {tx.TokenTransfers?.Count ?? 0}, Type: {tx.Type}, Desc: {tx.Description}");

                    var timestamp = DateTimeOffset.FromUnixTimeSeconds(tx.Timestamp).UtcDateTime;

                    // Handle Token Transfers
                    if (tx.TokenTransfers != null && tx.TokenTransfers.Any())
                    {
                        foreach (var transfer in tx.TokenTransfers)
                        {
                            if (transfer == null) continue;

                            Console.WriteLine($"SolanaService: Transfer - Mint: {transfer.Mint}, Amount: {transfer.Amount}, To: {transfer.ToUserAccount}, From: {transfer.FromUserAccount}");

                            if (transfer.Amount <= 0)
                            {
                                Console.WriteLine($"SolanaService: Skipping transfer with Amount <= 0");
                                continue;
                            }

                            string txType;
                            string asset = transfer.Mint == "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v" ? "USDC" : "SOL";

                            if (transfer.ToUserAccount == address)
                            {
                                txType = "Buy";
                            }
                            else if (transfer.FromUserAccount == address)
                            {
                                txType = "Sell";
                            }
                            else
                            {
                                Console.WriteLine($"SolanaService: Skipping irrelevant transfer");
                                continue;
                            }

                            var usdPrice = await _priceService.GetPriceAtTime(asset, timestamp);
                            Console.WriteLine($"SolanaService: Fetched USD price for {asset} at {timestamp}: {usdPrice}");

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
                        }
                    }

                    // Handle TRANSFER and SWAP via Description
                    if ((tx.Type == "TRANSFER" || tx.Type == "SWAP") && !string.IsNullOrEmpty(tx.Description))
                    {
                        Console.WriteLine($"SolanaService: Parsing Description: {tx.Description}");

                        decimal amount = 0;
                        string asset = null;
                        string txType = null;

                        // TRANSFER: USDC or SOL
                        if (tx.Type == "TRANSFER" && tx.Description.Contains($"{address} transferred"))
                        {
                            var usdcMatch = Regex.Match(tx.Description, $@"{address} transferred (\d+\.?\d*) USDC");
                            if (usdcMatch.Success && decimal.TryParse(usdcMatch.Groups[1].Value, out amount))
                            {
                                asset = "USDC";
                                txType = "Sell";
                                Console.WriteLine($"SolanaService: Detected USDC transfer of {amount} USDC from {address}");
                            }
                            var solMatch = Regex.Match(tx.Description, $@"(\d+\.?\d*) SOL.*to {address}");
                            if (solMatch.Success && decimal.TryParse(solMatch.Groups[1].Value, out amount))
                            {
                                asset = "SOL";
                                txType = "Buy";
                                Console.WriteLine($"SolanaService: Detected SOL transfer of {amount} SOL to {address}");
                            }
                        }
                        // SWAP: e.g., "C1GPhEkp... swapped X 9359LVZJs... for Y USDC/SOL"
                        else if (tx.Type == "SWAP" && tx.Description.Contains($"{address} swapped"))
                        {
                            var usdcSwapMatch = Regex.Match(tx.Description, $@"{address} swapped (\d+\.?\d*) (9359LVZJs8bf2FXcTdHvwcMnvg2ZCf6DUZ5ABDcJKx52) for (\d+\.?\d*) USDC");
                            if (usdcSwapMatch.Success && decimal.TryParse(usdcSwapMatch.Groups[3].Value, out amount))
                            {
                                asset = "USDC";
                                txType = "Buy";
                                var swapAmount = decimal.Parse(usdcSwapMatch.Groups[1].Value);
                                var swapAsset = usdcSwapMatch.Groups[2].Value;
                                var usdPrice = await _priceService.GetPriceAtTime(swapAsset, timestamp);

                                var sellTx = new SolanaTransaction
                                {
                                    Signature = tx.Signature,
                                    Timestamp = timestamp,
                                    Amount = swapAmount,
                                    Asset = swapAsset,
                                    Type = "Sell",
                                    WalletAddress = address,
                                    UsdValueAtTime = usdPrice * swapAmount
                                };
                                transactions.Add(sellTx);
                                Console.WriteLine($"SolanaService: Added sell tx - Asset: {swapAsset}, Amount: {swapAmount}");
                            }
                            var solSwapMatch = Regex.Match(tx.Description, $@"{address} swapped (\d+\.?\d*) (9359LVZJs8bf2FXcTdHvwcMnvg2ZCf6DUZ5ABDcJKx52) for (\d+\.?\d*) SOL");
                            if (solSwapMatch.Success && decimal.TryParse(solSwapMatch.Groups[3].Value, out amount))
                            {
                                asset = "SOL";
                                txType = "Buy";
                                var swapAmount = decimal.Parse(solSwapMatch.Groups[1].Value);
                                var swapAsset = solSwapMatch.Groups[2].Value;
                                var usdPrice = await _priceService.GetPriceAtTime(swapAsset, timestamp);

                                var sellTx = new SolanaTransaction
                                {
                                    Signature = tx.Signature,
                                    Timestamp = timestamp,
                                    Amount = swapAmount,
                                    Asset = swapAsset,
                                    Type = "Sell",
                                    WalletAddress = address,
                                    UsdValueAtTime = usdPrice * swapAmount
                                };
                                transactions.Add(sellTx);
                                Console.WriteLine($"SolanaService: Added sell tx - Asset: {swapAsset}, Amount: {swapAmount}");
                            }
                        }

                        if (txType != null && amount > 0 && asset != null)
                        {
                            var usdPrice = await _priceService.GetPriceAtTime(asset, timestamp);
                            Console.WriteLine($"SolanaService: Fetched USD price for {asset} at {timestamp}: {usdPrice}");

                            var solTx = new SolanaTransaction
                            {
                                Signature = tx.Signature,
                                Timestamp = timestamp,
                                Amount = amount,
                                Asset = asset,
                                Type = txType,
                                WalletAddress = address,
                                UsdValueAtTime = usdPrice * amount
                            };
                            transactions.Add(solTx);
                            Console.WriteLine($"SolanaService: Added tx - Asset: {asset}, Type: {txType}, Amount: {amount}");
                        }
                        else
                        {
                            Console.WriteLine($"SolanaService: No relevant amount/asset found in Description");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"SolanaService: Skipping tx type {tx.Type} with no relevant activity");
                    }
                }

                Console.WriteLine($"SolanaService: Fetched {transactions.Count} transactions, {stakingRewards.Count} staking rewards for {address}");
                _transactionsByAddress[address] = (transactions, stakingRewards);

                // Apply date range filter to the fetched transactions if needed
                if (startDate.HasValue || endDate.HasValue)
                {
                    transactions = transactions.Where(tx =>
                        (!startDate.HasValue || tx.Timestamp >= startDate.Value) &&
                        (!endDate.HasValue || tx.Timestamp <= endDate.Value)
                    ).ToList();
                    Console.WriteLine($"SolanaService: Filtered transactions to {transactions.Count} within date range");
                }

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
}