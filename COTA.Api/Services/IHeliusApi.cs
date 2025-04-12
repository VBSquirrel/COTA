using Refit;

namespace COTA.Api.Services;

public interface IHeliusApi
{
    [Get("/v0/addresses/{address}/transactions?api-key={apiKey}")]
    Task<List<HeliusTransaction>> GetTransactions(string address, string apiKey);
}