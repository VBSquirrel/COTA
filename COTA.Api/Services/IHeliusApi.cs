using COTA.Api.Services;
using Refit;

public interface IHeliusApi
{
    [Get("/v0/addresses/{address}/transactions")]
    Task<List<HeliusTransaction>> GetTransactions(string address, string apiKey, [Query] long? until = null, [Query] long? before = null);
}