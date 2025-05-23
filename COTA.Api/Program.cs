using Refit;
using Microsoft.Extensions.DependencyInjection;
using COTA.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Force TLS 1.2
System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddRefitClient<IHeliusApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.helius.xyz"));
builder.Services.AddRefitClient<ICoinGeckoApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.coingecko.com"));
builder.Services.AddSingleton<SolanaService>();
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddSingleton<TaxCalculator>(); // Add this line
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", builder =>
    {
        builder.WithOrigins("https://localhost:7243")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowClient");
app.UseSession();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();