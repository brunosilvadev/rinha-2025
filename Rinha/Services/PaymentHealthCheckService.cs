using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentHealthCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentHealthCheckService> _logger;
    private readonly string _defaultProcessorUrl;
    private readonly string _fallbackProcessorUrl;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PaymentHealthCheckService(IHttpClientFactory httpClientFactory, 
                                   ILogger<PaymentHealthCheckService> logger,
                                   string defaultProcessorUrl, 
                                   string fallbackProcessorUrl)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _defaultProcessorUrl = defaultProcessorUrl;
        _fallbackProcessorUrl = fallbackProcessorUrl;
    }

    /// <summary>
    /// Gets the health check status for the default payment processor.
    /// </summary>
    /// <returns>The health check result or null if failed</returns>
    public async Task<PaymentProcessorHealthCheck?> GetDefaultProcessorHealthAsync()
    {
        return await CallHealthCheckAsync(_defaultProcessorUrl, "default");
    }

    /// <summary>
    /// Gets the health check status for the fallback payment processor.
    /// </summary>
    /// <returns>The health check result or null if failed</returns>
    public async Task<PaymentProcessorHealthCheck?> GetFallbackProcessorHealthAsync()
    {
        return await CallHealthCheckAsync(_fallbackProcessorUrl, "fallback");
    }

    private async Task<PaymentProcessorHealthCheck?> CallHealthCheckAsync(string processorUrl, string processorType)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync($"{processorUrl}/payments/service-health");

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var healthCheck = JsonSerializer.Deserialize<PaymentProcessorHealthCheck>(jsonContent, _jsonOptions);

                _logger.LogDebug("{ProcessorType} processor health check successful: Failing={Failing}, MinResponseTime={MinResponseTime}ms",
                    processorType, healthCheck?.Failing, healthCheck?.MinResponseTime);

                return healthCheck;
            }
            else
            {
                _logger.LogWarning("{ProcessorType} processor health check failed with status: {StatusCode}",
                    processorType, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing health check for {ProcessorType} processor", processorType);
            return null;
        }
    }
}
