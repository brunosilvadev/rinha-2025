using System.Text;
using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentSummaryService _summaryService;
    private readonly string _defaultProcessorUrl;
    private readonly string _fallbackProcessorUrl;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PaymentService(IHttpClientFactory httpClientFactory, ILogger<PaymentService> logger,
        PaymentSummaryService summaryService, string defaultProcessorUrl, string fallbackProcessorUrl)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _summaryService = summaryService;
        _defaultProcessorUrl = defaultProcessorUrl;
        _fallbackProcessorUrl = fallbackProcessorUrl;
    }

    public async Task<bool> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        var paymentData = new
        {
            correlationId = paymentRequest.CorrelationId,
            amount = paymentRequest.Amount,
            requestedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        //TODO: improve logic to choose which processor to use
        // Try default processor first
        if (await TryProcessPayment(_defaultProcessorUrl, paymentData, "default", paymentRequest.Amount))
        {
            return true;
        }

        // If default fails, try fallback processor
        if (await TryProcessPayment(_fallbackProcessorUrl, paymentData, "fallback", paymentRequest.Amount))
        {
            return true;
        }

        _logger.LogError("Both payment processors failed for correlation ID: {CorrelationId}", 
            paymentRequest.CorrelationId);
        return false;
    }

    private async Task<bool> TryProcessPayment(string processorUrl, object paymentData, string processorType, decimal amount)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var json = JsonSerializer.Serialize(paymentData, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{processorUrl}/payments", content);

            if (response.IsSuccessStatusCode)
            {
                // Track successful payment in Redis
                await _summaryService.IncrementPaymentAsync(processorType, amount);

                _logger.LogInformation("Payment processed successfully via {ProcessorType} processor for correlation ID: {CorrelationId}",
                    processorType, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
                return true;
            }
            else
            {
                _logger.LogWarning("{ProcessorType} processor failed for correlation ID: {CorrelationId}. Status: {StatusCode}",
                    processorType, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData), response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ProcessorType} processor error for correlation ID: {CorrelationId}",
                processorType, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
            return false;
        }
    }
}
