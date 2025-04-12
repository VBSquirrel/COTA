using Microsoft.AspNetCore.Mvc;
using COTA.Api.Services;
using COTA.Core.Models;
using System.Text.Json;

namespace COTA.Api.Controllers;

[ApiController]
[Route("api/wallets")]
public class WalletsController : ControllerBase
{
    private readonly SolanaService _solanaService;
    private readonly ReportService _reportService;

    public WalletsController(SolanaService solanaService, ReportService reportService)
    {
        _solanaService = solanaService;
        _reportService = reportService;
    }

    [HttpGet("{address}/test")]
    public IActionResult Test(string address)
    {
        return Ok("API is working");
    }

    [HttpGet("{address}/taxes")]
    public async Task<ActionResult<TaxResponse>> GetTaxes(string address)
    {
        var transactions = await _solanaService.GetTransactions(address);
        var calculator = new TaxCalculator();
        var (capitalGains, income) = calculator.CalculateTaxes(transactions);

        HttpContext.Session.Set("CapitalGains", JsonSerializer.SerializeToUtf8Bytes(capitalGains));
        HttpContext.Session.Set("StakingRewards", JsonSerializer.SerializeToUtf8Bytes(income));

        return Ok(new TaxResponse { CapitalGains = capitalGains, StakingRewards = income });
    }

    [HttpGet("report/capital-gains")]
    public IActionResult DownloadCapitalGains()
    {
        var data = HttpContext.Session.Get("CapitalGains");
        if (data == null) return BadRequest("No capital gains data available.");

        var calculations = JsonSerializer.Deserialize<List<TaxCalculation>>(data);
        var filePath = Path.GetTempFileName() + ".csv";
        _reportService.ExportCapitalGains(calculations!, filePath);
        return PhysicalFile(filePath, "text/csv", "capital_gains.csv");
    }

    [HttpGet("report/staking-income")]
    public IActionResult DownloadStakingIncome()
    {
        var data = HttpContext.Session.Get("StakingRewards");
        if (data == null) return BadRequest("No staking rewards data available.");

        var rewards = JsonSerializer.Deserialize<List<StakingReward>>(data);
        var filePath = Path.GetTempFileName() + ".csv";
        _reportService.ExportStakingIncome(rewards!, filePath);
        return PhysicalFile(filePath, "text/csv", "staking_income.csv");
    }
}

public class TaxResponse
{
    public List<TaxCalculation>? CapitalGains { get; set; }
    public List<StakingReward>? StakingRewards { get; set; }
}