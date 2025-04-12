using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using COTA.Core.Models;
using COTA.Api.Services;

namespace COTA.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WalletsController : ControllerBase
{
    private readonly SolanaService _solanaService;

    public WalletsController(SolanaService solanaService)
    {
        _solanaService = solanaService;
    }

    [HttpGet("{address}/taxes")]
    public async Task<ActionResult<(List<TaxCalculation>, List<StakingReward>)>> GetTaxes(string address)
    {
        try
        {
            if (string.IsNullOrEmpty(address) || address.Length != 44)
            {
                Console.WriteLine($"WalletsController: Invalid address {address}");
                return BadRequest("Invalid wallet address.");
            }

            Console.WriteLine($"WalletsController: Processing taxes for {address}");
            var transactions = await _solanaService.GetTransactions(address);
            Console.WriteLine($"WalletsController: Retrieved {transactions.Count} transactions");

            var calculator = new TaxCalculator();
            var (capitalGains, income) = calculator.CalculateTaxes(transactions);
            Console.WriteLine($"WalletsController: Calculated {capitalGains.Count} capital gains, {income.Count} staking income");

            HttpContext.Session.Set("CapitalGains", JsonSerializer.SerializeToUtf8Bytes(capitalGains));
            HttpContext.Session.Set("StakingRewards", JsonSerializer.SerializeToUtf8Bytes(income));

            return Ok((capitalGains, income));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WalletsController: Error calculating taxes for {address}: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, "Failed to calculate taxes.");
        }
    }

    [HttpGet("report/capital-gains")]
    public IActionResult GetCapitalGainsReport()
    {
        try
        {
            var capitalGainsBytes = HttpContext.Session.Get("CapitalGains");
            if (capitalGainsBytes == null)
            {
                Console.WriteLine("WalletsController: No capital gains data in session.");
                return NotFound("No capital gains data available.");
            }

            var capitalGains = JsonSerializer.Deserialize<List<TaxCalculation>>(capitalGainsBytes);
            if (capitalGains == null)
            {
                Console.WriteLine("WalletsController: Failed to deserialize capital gains.");
                return StatusCode(500, "Failed to process capital gains data.");
            }

            var csv = GenerateCsv(capitalGains);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "capital_gains.csv");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WalletsController: Error generating capital gains CSV: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, "Failed to generate report.");
        }
    }

    [HttpGet("report/staking-income")]
    public IActionResult GetStakingIncomeReport()
    {
        try
        {
            var stakingRewardsBytes = HttpContext.Session.Get("StakingRewards");
            if (stakingRewardsBytes == null)
            {
                Console.WriteLine("WalletsController: No staking rewards data in session.");
                return NotFound("No staking rewards data available.");
            }

            var stakingRewards = JsonSerializer.Deserialize<List<StakingReward>>(stakingRewardsBytes);
            if (stakingRewards == null)
            {
                Console.WriteLine("WalletsController: Failed to deserialize staking rewards.");
                return StatusCode(500, "Failed to process staking rewards data.");
            }

            var csv = GenerateCsv(stakingRewards);
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "staking_income.csv");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WalletsController: Error generating staking income CSV: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, "Failed to generate report.");
        }
    }

    private string GenerateCsv<T>(IEnumerable<T> items)
    {
        var csv = new System.Text.StringBuilder();
        if (typeof(T) == typeof(TaxCalculation))
        {
            csv.AppendLine("Asset,CostBasis,Proceeds,GainOrLoss,IsShortTerm");
            foreach (var item in items.Cast<TaxCalculation>())
            {
                csv.AppendLine($"{item.Asset},{item.CostBasis:F2},{item.Proceeds:F2},{item.GainOrLoss:F2},{item.IsShortTerm}");
            }
        }
        else if (typeof(T) == typeof(StakingReward))
        {
            csv.AppendLine("WalletAddress,Amount,DateReceived,UsdValueAtTime");
            foreach (var item in items.Cast<StakingReward>())
            {
                csv.AppendLine($"{item.WalletAddress},{item.Amount:F8},{item.Timestamp:yyyy-MM-dd},{item.UsdValueAtTime:F2}");
            }
        }
        return csv.ToString();
    }
}