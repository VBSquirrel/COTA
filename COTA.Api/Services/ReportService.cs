using CsvHelper;
using System.Globalization;
using COTA.Core.Models;

namespace COTA.Api.Services;

public class ReportService
{
    public void ExportCapitalGains(List<TaxCalculation> calculations, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(calculations.Select(c => new
        {
            c.Asset,
            c.CostBasis,
            c.Proceeds,
            GainLoss = c.GainOrLoss,
            ShortTerm = c.IsShortTerm ? "Yes" : "No"
        }));
    }

    public void ExportStakingIncome(List<StakingReward> rewards, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rewards.Select(r => new
        {
            r.WalletAddress,
            r.Amount,
            DateReceived = r.Timestamp,
            r.UsdValueAtTime
        }));
    }
}