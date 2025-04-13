// File: COTA.Api/Services/TaxCalculator.cs
using COTA.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace COTA.Api.Services
{
    public class TaxCalculator
    {
        public (List<TaxCalculation>, List<StakingReward>) CalculateTaxes(List<SolanaTransaction> transactions, List<StakingReward> stakingRewards)
        {
            Console.WriteLine($"TaxCalculator: Calculating taxes for {transactions.Count} transactions and {stakingRewards.Count} staking rewards");

            var capitalGains = new List<TaxCalculation>();
            var orderedTransactions = transactions.OrderBy(t => t.Timestamp).ThenBy(t => t.Signature).ToList();
            var assetInventories = new Dictionary<string, List<(decimal Amount, decimal? UsdValue, DateTime Timestamp)>>();

            for (int i = 0; i < orderedTransactions.Count; i++)
            {
                var tx = orderedTransactions[i];
                Console.WriteLine($"TaxCalculator: Processing tx {tx.Signature}, Type: {tx.Type}, Asset: {tx.Asset}, Amount: {tx.Amount}, USD: {tx.UsdValueAtTime}");

                if (!assetInventories.ContainsKey(tx.Asset))
                {
                    assetInventories[tx.Asset] = new List<(decimal Amount, decimal? UsdValue, DateTime Timestamp)>();
                }

                if (tx.Type == "Buy")
                {
                    assetInventories[tx.Asset].Add((tx.Amount, tx.UsdValueAtTime, tx.Timestamp));
                    Console.WriteLine($"TaxCalculator: Added to {tx.Asset} inventory: {tx.Amount} at ${tx.UsdValueAtTime}");

                    // Check for swap: next tx with same Signature, Type=Sell
                    if (i + 1 < orderedTransactions.Count)
                    {
                        var nextTx = orderedTransactions[i + 1];
                        if (nextTx.Signature == tx.Signature && nextTx.Type == "Sell" && nextTx.Asset != tx.Asset)
                        {
                            var gain = new TaxCalculation
                            {
                                Asset = nextTx.Asset,
                                CostBasis = 0, // Assume no basis for swapped-out asset
                                Proceeds = nextTx.UsdValueAtTime ?? 0m, // Handle null
                                GainOrLoss = nextTx.UsdValueAtTime ?? 0m,
                                IsShortTerm = true
                            };
                            capitalGains.Add(gain);
                            Console.WriteLine($"TaxCalculator: Added swap gain - Asset: {gain.Asset}, Gain: {gain.GainOrLoss}");
                            i++; // Skip next tx
                        }
                    }
                }
                else if (tx.Type == "Sell")
                {
                    var inventory = assetInventories[tx.Asset];
                    decimal proceeds = tx.UsdValueAtTime ?? 0m; // Handle null
                    decimal costBasis = 0;

                    if (inventory.Count == 0)
                    {
                        Console.WriteLine($"TaxCalculator: No inventory for {tx.Asset}, assuming cost basis $0");
                    }
                    else
                    {
                        decimal amountToSell = tx.Amount;
                        while (amountToSell > 0 && inventory.Count > 0)
                        {
                            var (invAmount, invUsdValue, invTimestamp) = inventory[0];
                            decimal usdValue = invUsdValue ?? 0m; // Handle null
                            if (invAmount <= amountToSell)
                            {
                                costBasis += usdValue;
                                amountToSell -= invAmount;
                                inventory.RemoveAt(0);
                            }
                            else
                            {
                                decimal fraction = amountToSell / invAmount;
                                costBasis += usdValue * fraction;
                                inventory[0] = (invAmount - amountToSell, usdValue * (1 - fraction), invTimestamp);
                                amountToSell = 0;
                            }
                        }
                    }

                    var gain = new TaxCalculation
                    {
                        Asset = tx.Asset,
                        CostBasis = costBasis,
                        Proceeds = proceeds,
                        GainOrLoss = proceeds - costBasis,
                        IsShortTerm = inventory.Any() ? (tx.Timestamp - inventory.First().Timestamp).TotalDays <= 365 : true
                    };
                    capitalGains.Add(gain);
                    Console.WriteLine($"TaxCalculator: Added gain - Asset: {gain.Asset}, Cost: {gain.CostBasis}, Proceeds: {gain.Proceeds}, Gain: {gain.GainOrLoss}");
                }
            }

            Console.WriteLine($"TaxCalculator: Generated {capitalGains.Count} capital gains, {stakingRewards.Count} staking income");
            return (capitalGains, stakingRewards);
        }
    }
}