using Rinha.Endpoints;
using Rinha.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Get configuration from environment variables
var defaultProcessorUrl = Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL") ?? "http://payment-processor-default:8080";
var fallbackProcessorUrl = Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL") ?? "http://payment-processor-fallback:8080";
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "localhost:6379";

// Configure Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectRetry = 3;
    configuration.ConnectTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.AddHttpClient<PaymentService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(1);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    MaxConnectionsPerServer = 100,
    UseProxy = false
});
builder.Services.AddSingleton<PaymentSummaryService>();
builder.Services.AddSingleton(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<PaymentHealthCheckService>>();
    return new PaymentHealthCheckService(httpClientFactory, logger, defaultProcessorUrl, fallbackProcessorUrl);
});
builder.Services.AddSingleton(provider =>
{
    var healthCheckService = provider.GetRequiredService<PaymentHealthCheckService>();
    var logger = provider.GetRequiredService<ILogger<DecisionService>>();
    var redis = provider.GetRequiredService<IConnectionMultiplexer>();
    return new DecisionService(healthCheckService, logger, redis);
});
builder.Services.AddSingleton(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<PaymentService>>();
    var summaryService = provider.GetRequiredService<PaymentSummaryService>();
    var decisionService = provider.GetRequiredService<DecisionService>();
    return new PaymentService(httpClientFactory, logger, summaryService, decisionService, defaultProcessorUrl, fallbackProcessorUrl);
});

var app = builder.Build();

app.MapGet("/", () => "It's time for Rinha 2025!");

app.MapPaymentEndpoints();

app.Run();
