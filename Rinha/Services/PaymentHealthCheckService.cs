using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentHealthCheckService(IHttpClientFactory httpClientFactory,
                               ILogger<PaymentHealthCheckService> logger,
                               string defaultProcessorUrl,
                               string fallbackProcessorUrl)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<PaymentHealthCheckService> _logger = logger;
    private readonly string _defaultProcessorUrl = defaultProcessorUrl;
    private readonly string _fallbackProcessorUrl = fallbackProcessorUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Gets the health check status for the default payment processor.
    public async Task<PaymentProcessorHealthCheck?> GetDefaultProcessorHealthAsync()
    {
        return await CallHealthCheckAsync(_defaultProcessorUrl, "default");
    }

    // Gets the health check status for the fallback payment processor.
    public async Task<PaymentProcessorHealthCheck?> GetFallbackProcessorHealthAsync()
    {
        return await CallHealthCheckAsync(_fallbackProcessorUrl, "fallback");
    }

    private async Task<PaymentProcessorHealthCheck?> CallHealthCheckAsync(string processorUrl, string processorType)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("HealthCheck");
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var response = await httpClient.GetAsync($"{processorUrl}/payments/service-health", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync(cts.Token);
                var healthCheck = JsonSerializer.Deserialize<PaymentProcessorHealthCheck>(jsonContent, JsonOptions);

                _logger.LogDebug("{ProcessorType} processor health check successful: Failing={Failing}, MinResponseTime={MinResponseTime}ms",
                    processorType, healthCheck?.Failing, healthCheck?.MinResponseTime);

                return healthCheck;
            }

            _logger.LogWarning("{ProcessorType} processor health check failed with status: {StatusCode}",
                processorType, response.StatusCode);
            return null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Health check timeout for {ProcessorType} processor", processorType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check for {ProcessorType} processor", processorType);
            return null;
        }
    }
}
