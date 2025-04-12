using Refit;

namespace COTA.Api.Services;

public interface ICoinGeckoApi
{
    [Get("/api/v3/coins/{coinId}/history?date={date}")]
    Task<CoinGeckoPriceResponse> GetHistoricalPrice(string coinId, string date);
}

public class CoinGeckoPriceResponse
{
    public MarketData? Market_Data { get; set; }
}

public class MarketData
{
    public Dictionary<string, decimal>? Current_Price { get; set; }
}

public class PriceService
{
    private readonly ICoinGeckoApi _coinGeckoApi;

    public PriceService(ICoinGeckoApi coinGeckoApi)
    {
        _coinGeckoApi = coinGeckoApi;
    }

    public async Task<decimal> GetPriceAtTime(string? asset, DateTime date)
    {
        if (string.IsNullOrEmpty(asset)) return 0;
        var coinId = asset == "SOL" ? "solana" : asset.ToLower();
        var dateStr = date.ToString("dd-MM-yyyy");
        try
        {
            var response = await _coinGeckoApi.GetHistoricalPrice(coinId, dateStr);
            return response.Market_Data?.Current_Price?["usd"] ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}