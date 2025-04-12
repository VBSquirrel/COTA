using Refit;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHttpClient();
services.AddRefitClient<IHeliusApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.helius.io"));
Console.WriteLine("Refit works!");

interface IHeliusApi
{
    [Get("/")]
    Task<string> Get();
}