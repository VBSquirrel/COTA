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
            }
        }

        return (capitalGains, income);
    }
}