using Rinha.Endpoints;
using Rinha.Services;

var builder = WebApplication.CreateBuilder(args);

// Get Payment Processor URLs from environment variables
var defaultProcessorUrl = Environment.GetEnvironmentVariable("PROCESSOR_DEFAULT_URL") ?? "http://payment-processor-default:8080";
var fallbackProcessorUrl = Environment.GetEnvironmentVariable("PROCESSOR_FALLBACK_URL") ?? "http://payment-processor-fallback:8080";

// Register HttpClient and PaymentService
builder.Services.AddHttpClient<PaymentService>();
builder.Services.AddSingleton<PaymentService>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var logger = provider.GetRequiredService<ILogger<PaymentService>>();
    return new PaymentService(httpClientFactory, logger, defaultProcessorUrl, fallbackProcessorUrl);
});

var app = builder.Build();

app.MapGet("/", () => "It's time for Rinha 2025!");

// Register payment endpoints
app.MapPaymentEndpoints();

app.Run();
