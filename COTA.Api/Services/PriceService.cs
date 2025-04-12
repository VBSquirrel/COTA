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
            Console.WriteLine("PriceService: Loading coin list...");
            var coins = await _coinGeckoApi.GetCoinList();
            foreach (var coin in coins)
            {
                if (!string.IsNullOrEmpty(coin.Symbol) && !string.IsNullOrEmpty(coin.Id))
                {
                    // Prefer solana for SOL
                    if (coin.Symbol.ToUpper() == "SOL" && coin.Id != "solana")
                    {
                        continue;
                    }
                    _assetToCoinIdCache.AddOrUpdate(coin.Symbol.ToUpper(), coin.Id, (key, oldValue) => coin.Id);
                }
            }
            _coinListLoaded = true;
            Console.WriteLine($"PriceService: Loaded {_assetToCoinIdCache.Count} coin mappings.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PriceService: Failed to load coin list: {ex.Message}");
            _assetToCoinIdCache.TryAdd("SOL", "solana");
            _coinListLoaded = true;
        }
    }

    public async Task<decimal> GetPriceAtTime(string? asset, DateTime date)
    {
        Console.WriteLine($"PriceService: Fetching price for asset '{asset}' on {date:dd-MM-yyyy}");
        if (string.IsNullOrEmpty(asset))
        {
            Console.WriteLine("PriceService: Asset is null or empty, defaulting to SOL.");
            asset = "SOL";
        }

        await LoadCoinListAsync();

        if (!_assetToCoinIdCache.TryGetValue(asset.ToUpper(), out var coinId))
        {
            Console.WriteLine($"PriceService: Asset '{asset}' not found, defaulting to SOL.");
            coinId = "solana";
        }

        if (date.Date > DateTime.UtcNow.Date)
        {
            Console.WriteLine($"PriceService: Date '{date:dd-MM-yyyy}' is in the future, using today.");
            date = DateTime.UtcNow;
        }

        var dateStr = date.ToString("dd-MM-yyyy");
        for (int retry = 0; retry < 3; retry++)
        {
            try
            {
                var response = await _coinGeckoApi.GetHistoricalPrice(coinId, dateStr);
                var price = response.Market_Data?.Current_Price?["usd"] ?? 0;
                Console.WriteLine($"PriceService: Fetched price for '{coinId}' on {dateStr}: ${price}");
                return price;
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"PriceService: 401 Unauthorized for '{coinId}' on {dateStr}.");
                return 0;
            }
            catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine($"PriceService: 429 Rate limit hit for '{coinId}' on {dateStr}, retrying ({retry + 1}/3)...");
                await Task.Delay(2000 * (retry + 1));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PriceService: Error fetching price for '{coinId}' on {dateStr}: {ex.Message}");
                return 0;
            }
        }

        Console.WriteLine($"PriceService: Failed to fetch price for '{coinId}' on {dateStr} after retries.");
        return 0;
    }
}