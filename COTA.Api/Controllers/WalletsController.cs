using COTA.Api.Services;
using COTA.Core.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using CsvHelper;
using System.Globalization;

namespace COTA.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WalletsController : ControllerBase
    {
        private readonly SolanaService _solanaService;
        private readonly TaxCalculator _taxCalculator;

        public WalletsController(SolanaService solanaService, TaxCalculator taxCalculator)
        {
            _solanaService = solanaService;
            _taxCalculator = taxCalculator;
        }

        [HttpPost("taxes")]
        public async Task<ActionResult<(List<TaxCalculation>, List<StakingReward>)>> GetTaxes([FromBody] string[] addresses)
        {
            try
            {
                if (addresses == null || !addresses.Any() || addresses.Any(a => string.IsNullOrEmpty(a) || a.Length != 44))
                {
                    Console.WriteLine("WalletsController: Invalid or empty addresses");
                    return BadRequest("Invalid wallet addresses.");
                }

                Console.WriteLine($"WalletsController: Processing taxes for {string.Join(", ", addresses)}");
                var allTransactions = new List<SolanaTransaction>();
                var allStakingRewards = new List<StakingReward>();

                foreach (var address in addresses)
                {
                    var transactions = await _solanaService.GetTransactions(address);
                    if (transactions == null)
                    {
                        Console.WriteLine($"WalletsController: GetTransactions returned null for {address}");
                        return StatusCode(500, $"Failed to fetch transactions for {address}");
                    }

                    var stakingRewards = await _solanaService.GetStakingRewards(address);
                    if (stakingRewards == null)
                    {
                        Console.WriteLine($"WalletsController: GetStakingRewards returned null for {address}");
                        return StatusCode(500, $"Failed to fetch staking rewards for {address}");
                    }

                    allTransactions.AddRange(transactions);
                    allStakingRewards.AddRange(stakingRewards);
                    Console.WriteLine($"WalletsController: Retrieved {transactions.Count} transactions for {address}");
                }

                if (!allTransactions.Any() && !allStakingRewards.Any())
                {
                    Console.WriteLine("WalletsController: No transactions or staking rewards found for any address");
                    return Ok(new { CapitalGains = new List<TaxCalculation>(), StakingRewards = new List<StakingReward>() });
                }

                var (capitalGains, calculatedStakingRewards) = _taxCalculator.CalculateTaxes(allTransactions, allStakingRewards);
                if (capitalGains == null || calculatedStakingRewards == null)
                {
                    Console.WriteLine("WalletsController: CalculateTaxes returned null results");
                    return StatusCode(500, "Failed to calculate taxes: null results");
                }

                Console.WriteLine($"WalletsController: Calculated {capitalGains.Count} capital gains, {calculatedStakingRewards.Count} staking income");

                HttpContext.Session.SetString("CapitalGains", JsonSerializer.Serialize(capitalGains));
                HttpContext.Session.SetString("StakingRewards", JsonSerializer.Serialize(calculatedStakingRewards));

                return Ok(new { CapitalGains = capitalGains, StakingRewards = calculatedStakingRewards });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WalletsController: Error calculating taxes: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Failed to calculate taxes: {ex.Message}");
            }
        }

        [HttpGet("report/capital-gains")]
        public IActionResult GetCapitalGainsReport()
        {
            try
            {
                var capitalGainsJson = HttpContext.Session.GetString("CapitalGains");
                if (string.IsNullOrEmpty(capitalGainsJson))
                {
                    Console.WriteLine("WalletsController: No capital gains data in session.");
                    return NotFound("No capital gains data available.");
                }

                var capitalGains = JsonSerializer.Deserialize<List<TaxCalculation>>(capitalGainsJson);
                if (capitalGains == null)
                {
                    Console.WriteLine("WalletsController: Failed to deserialize capital gains.");
                    return StatusCode(500, "Failed to process capital gains data.");
                }

                var memoryStream = new MemoryStream();
                var writer = new StreamWriter(memoryStream, Encoding.UTF8);
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(capitalGains.Select(c => new
                    {
                        c.Asset,
                        c.CostBasis,
                        c.Proceeds,
                        GainLoss = c.GainOrLoss,
                        ShortTerm = c.IsShortTerm ? "Yes" : "No"
                    }));
                }
                writer.Flush();
                memoryStream.Position = 0;

                Console.WriteLine($"WalletsController: Generated capital gains CSV with {capitalGains.Count} entries");

                return File(memoryStream, "text/csv", "capital_gains.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WalletsController: Error generating capital gains CSV: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Failed to generate report: {ex.Message}");
            }
        }

        [HttpGet("report/staking-income")]
        public IActionResult GetStakingIncomeReport()
        {
            try
            {
                var stakingRewardsJson = HttpContext.Session.GetString("StakingRewards");
                if (string.IsNullOrEmpty(stakingRewardsJson))
                {
                    Console.WriteLine("WalletsController: No staking rewards data in session.");
                    return NotFound("No staking rewards data available.");
                }

                var stakingRewards = JsonSerializer.Deserialize<List<StakingReward>>(stakingRewardsJson);
                if (stakingRewards == null)
                {
                    Console.WriteLine("WalletsController: Failed to deserialize staking rewards.");
                    return StatusCode(500, "Failed to process staking rewards data.");
                }

                var memoryStream = new MemoryStream();
                var writer = new StreamWriter(memoryStream, Encoding.UTF8);
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(stakingRewards.Select(r => new
                    {
                        r.WalletAddress,
                        r.Amount,
                        DateReceived = r.Timestamp,
                        r.UsdValueAtTime
                    }));
                }
                writer.Flush();
                memoryStream.Position = 0;

                Console.WriteLine($"WalletsController: Generated staking income CSV with {stakingRewards.Count} entries");

                return File(memoryStream, "text/csv", "staking_income.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WalletsController: Error generating staking income CSV: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, $"Failed to generate report: {ex.Message}");
            }
        }
    }
}