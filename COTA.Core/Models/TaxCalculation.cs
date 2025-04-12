namespace COTA.Core.Models;

public class TaxCalculation
{
    public string? Asset { get; set; }
    public decimal CostBasis { get; set; }
    public decimal Proceeds { get; set; }
    public decimal GainOrLoss { get; set; }
    public bool IsShortTerm { get; set; }
}