namespace COTA.Core.Models;

public class TaxCalculator
{
    public (List<TaxCalculation> CapitalGains, List<StakingReward> Income) CalculateTaxes(List<SolanaTransaction> transactions)
    {
        var capitalGains = new List<TaxCalculation>();
        var income = new List<StakingReward>();
        var inventory = new Queue<SolanaTransaction>();

        foreach (var tx in transactions.OrderBy(t => t.Timestamp))
        {
            if (tx.Type == "Buy" || tx.Type == "StakeReward")
            {
                if (tx.Amount <= 0 || tx.UsdValueAtTime <= 0)
                {
                    Console.WriteLine($"TaxCalculator: Skipping {tx.Type} tx {tx.Signature} with Amount: {tx.Amount}, UsdValueAtTime: {tx.UsdValueAtTime}");
                    continue;
                }

                inventory.Enqueue(tx);
                if (tx.Type == "StakeReward")
                {
                    income.Add(new StakingReward
                    {
                        WalletAddress = tx.WalletAddress,
                        Amount = tx.Amount,
                        Timestamp = tx.Timestamp,
                        UsdValueAtTime = tx.UsdValueAtTime ?? 0
                    });
                }
            }
            else if (tx.Type == "Sell")
            {
                if (tx.Amount <= 0 || tx.UsdValueAtTime <= 0)
                {
                    Console.WriteLine($"TaxCalculator: Skipping Sell tx {tx.Signature} with Amount: {tx.Amount}, UsdValueAtTime: {tx.UsdValueAtTime}");
                    continue;
                }

                if (inventory.TryDequeue(out var buyTx))
                {
                    var gainOrLoss = (tx.UsdValueAtTime ?? 0) - (buyTx.UsdValueAtTime ?? 0);
                    var holdingPeriod = tx.Timestamp - buyTx.Timestamp;

                    capitalGains.Add(new TaxCalculation
                    {
                        Asset = tx.Asset,
                        CostBasis = buyTx.UsdValueAtTime ?? 0,
                        Proceeds = tx.UsdValueAtTime ?? 0,
                        GainOrLoss = gainOrLoss,
                        IsShortTerm = holdingPeriod.TotalDays <= 365
                    });
                }
                else
                {
                    Console.WriteLine($"TaxCalculator: No buy tx available for Sell tx {tx.Signature}");
                }
            }
            else
            {
                Console.WriteLine($"TaxCalculator: Skipping tx {tx.Signature} with unknown Type: {tx.Type}");
            }
        }

        Console.WriteLine($"TaxCalculator: Generated {capitalGains.Count} capital gains, {income.Count} staking income");
        return (capitalGains, income);
    }
}