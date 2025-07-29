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

// Register services
builder.Services.AddHttpClient<PaymentService>();
builder.Services.AddSingleton<PaymentSummaryService>();
builder.Services.AddSingleton<PaymentService>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<PaymentService>>();
    var summaryService = provider.GetRequiredService<PaymentSummaryService>();
    return new PaymentService(httpClientFactory, logger, summaryService, defaultProcessorUrl, fallbackProcessorUrl);
});

var app = builder.Build();

app.MapGet("/", () => "It's time for Rinha 2025!");

// Register payment endpoints
app.MapPaymentEndpoints();

app.Run();
