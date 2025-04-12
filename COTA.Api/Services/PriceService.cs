using Refit;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace COTA.Api.Services;

public interface ICoinGeckoApi
{
    [Get("/api/v3/coins/{coinId}/history?date={date}")]
    Task<CoinGeckoPriceResponse> GetHistoricalPrice(string coinId, string date);

    [Get("/api/v3/coins/list")]
    Task<List<CoinListItem>> GetCoinList();
}

public class CoinListItem
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
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
    private readonly ConcurrentDictionary<string, string> _assetToCoinIdCache = new();
    private bool _coinListLoaded = false;

    public PriceService(ICoinGeckoApi coinGeckoApi)
    {
        _coinGeckoApi = coinGeckoApi;
    }

    private async Task LoadCoinListAsync()
    {
        if (_coinListLoaded) return;

        try
        {
            var coins = await _coinGeckoApi.GetCoinList();
            foreach (var coin in coins)
            {
                if (!string.IsNullOrEmpty(coin.Symbol) && !string.IsNullOrEmpty(coin.Id))
                {
                    _assetToCoinIdCache.TryAdd(coin.Symbol.ToUpper(), coin.Id);
                }
            }
            _coinListLoaded = true;
            Debug.WriteLine($"PriceService: Loaded {_assetToCoinIdCache.Count} coin mappings.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PriceService: Failed to load coin list: {ex.Message}");
            // Fallback mapping
            _assetToCoinIdCache.TryAdd("SOL", "solana");
            _coinListLoaded = true;
        }
    }

    public async Task<decimal> GetPriceAtTime(string? asset, DateTime date)
    {
        if (string.IsNullOrEmpty(asset))
        {
            Debug.WriteLine("PriceService: Asset is null or empty, defaulting to SOL.");
            asset = "SOL";
        }

        // Ensure coin list is loaded
        await LoadCoinListAsync();

        // Map asset to CoinGecko coin ID
        if (!_assetToCoinIdCache.TryGetValue(asset.ToUpper(), out var coinId))
        {
            Debug.WriteLine($"PriceService: Asset '{asset}' not found in coin list, defaulting to SOL.");
            coinId = "solana"; // Default to SOL
        }

        // Cap future dates
        if (date.Date > DateTime.UtcNow.Date)
        {
            Debug.WriteLine($"PriceService: Date '{date:dd-MM-yyyy}' is in the future, using today.");
            date = DateTime.UtcNow;
        }

        var dateStr = date.ToString("dd-MM-yyyy");
        try
        {
            var response = await _coinGeckoApi.GetHistoricalPrice(coinId, dateStr);
            var price = response.Market_Data?.Current_Price?["usd"] ?? 0;
            Debug.WriteLine($"PriceService: Fetched price for '{coinId}' on {dateStr}: ${price}");
            return price;
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            Debug.WriteLine($"PriceService: 404 for '{coinId}' on {dateStr}, returning 0.");
            return 0;
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            Debug.WriteLine($"PriceService: Rate limit hit for '{coinId}' on {dateStr}, returning 0.");
            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PriceService: Error fetching price for '{coinId}' on {dateStr}: {ex.Message}");
            return 0;
        }
    }
}