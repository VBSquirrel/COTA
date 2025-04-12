using Refit;
using Microsoft.Extensions.DependencyInjection;
using COTA.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();
builder.Services.AddRefitClient<IHeliusApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.helius.io"));
builder.Services.AddRefitClient<ICoinGeckoApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.coingecko.com"));
builder.Services.AddSingleton<SolanaService>();
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSession();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();