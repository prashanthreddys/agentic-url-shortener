using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using UrlShortener.Core.Abstractions;
using UrlShortener.Core.Mongo;
using UrlShortener.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Core service wiring.
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton(new ShorteningOptions());
builder.Services.AddSingleton(sp => new UrlValidator(sp.GetRequiredService<ShorteningOptions>().BlockPrivateHosts));

// MongoDB wiring.
var mongoOptions = new MongoOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("Mongo") ?? "mongodb://localhost:27017",
    Database = builder.Configuration["Persistence:Database"] ?? "urlshortener",
};
builder.Services.AddSingleton(mongoOptions);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoOptions.ConnectionString));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.Database));
builder.Services.AddScoped<IShortUrlRepository, MongoShortUrlRepository>();

// Distributed, atomic counter (the Zookeeper analog) provides collision-free code ranges.
builder.Services.AddSingleton<IRangeAllocator>(sp =>
    new MongoRangeAllocator(sp.GetRequiredService<IMongoDatabase>(), mongoOptions));
builder.Services.AddSingleton<ICounterRangeProvider>(sp =>
    new RangeCounterProvider(sp.GetRequiredService<IRangeAllocator>(), rangeSize: 1000));
builder.Services.AddSingleton<IShortCodeGenerator, CounterShortCodeGenerator>();

builder.Services.AddScoped<UrlShorteningService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Reliability guardrail: fixed-window rate limiting on link creation.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("create", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// Prepare the MongoDB indexes on startup.
using (var scope = app.Services.CreateScope())
{
    MongoInitializer.EnsureIndexes(
        scope.ServiceProvider.GetRequiredService<IMongoDatabase>(),
        scope.ServiceProvider.GetRequiredService<MongoOptions>());
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Exposed so integration tests can reference the entry-point assembly.
public partial class Program { }
