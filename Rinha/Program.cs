using Rinha.Endpoints;
using Rinha.Services;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClient and PaymentService
builder.Services.AddHttpClient<PaymentService>(client =>
{
    // Configure base address for your external API
    client.BaseAddress = new Uri("https://your-external-api.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.MapGet("/", () => "It's time for Rinha 2025!");

// Register payment endpoints
app.MapPaymentEndpoints();

app.Run();
