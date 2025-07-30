using System.Text;
using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentService(IHttpClientFactory httpClientFactory, ILogger<PaymentService> logger,
    PaymentSummaryService summaryService, DecisionService decisionService, string defaultProcessorUrl, string fallbackProcessorUrl)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<PaymentService> _logger = logger;
    private readonly PaymentSummaryService _summaryService = summaryService;
    private readonly DecisionService _decisionService = decisionService;
    private readonly string _defaultProcessorUrl = defaultProcessorUrl;
    private readonly string _fallbackProcessorUrl = fallbackProcessorUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = 
    {
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    };

    public async Task<bool> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        var paymentData = new
        {
            correlationId = paymentRequest.CorrelationId,
            amount = paymentRequest.Amount,
            requestedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Retry the entire payment process with exponential backoff
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            bool success = await TryProcessPaymentWithFallback(paymentData, paymentRequest.Amount, attempt);
            if (success)
            {
                return true;
            }

            // If this wasn't the last attempt, wait before retrying
            if (attempt < MaxRetries - 1)
            {
                var delay = RetryDelays[attempt];
                _logger.LogWarning("Payment attempt {Attempt} failed for correlation ID: {CorrelationId}, retrying in {Delay}ms", 
                    attempt + 1, paymentRequest.CorrelationId, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }

        _logger.LogError("Payment failed after {MaxRetries} attempts for correlation ID: {CorrelationId}", 
            MaxRetries, paymentRequest.CorrelationId);
        return false;
    }

    private async Task<bool> TryProcessPaymentWithFallback(object paymentData, decimal amount, int attemptNumber)
    {
        // Use DecisionService to determine the preferred processor
        bool preferDefaultProcessor = await _decisionService.DecidePaymentProcessor();
        
        if (preferDefaultProcessor)
        {
            // Try default processor first, fallback to fallback processor if it fails
            if (await TryProcessPayment(_defaultProcessorUrl, paymentData, "default", amount))
            {
                return true;
            }
            
            _logger.LogWarning("Default processor failed despite being recommended (attempt {Attempt}), trying fallback for correlation ID: {CorrelationId}", 
                attemptNumber + 1, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
            
            // Fallback to the other processor
            if (await TryProcessPayment(_fallbackProcessorUrl, paymentData, "fallback", amount))
            {
                return true;
            }
        }
        else
        {
            // Try fallback processor first, fallback to default processor if it fails
            if (await TryProcessPayment(_fallbackProcessorUrl, paymentData, "fallback", amount))
            {
                return true;
            }
            
            _logger.LogWarning("Fallback processor failed despite being recommended (attempt {Attempt}), trying default for correlation ID: {CorrelationId}", 
                attemptNumber + 1, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
            
            // Fallback to the other processor
            if (await TryProcessPayment(_defaultProcessorUrl, paymentData, "default", amount))
            {
                return true;
            }
        }

        _logger.LogWarning("Both processors failed on attempt {Attempt} for correlation ID: {CorrelationId}", 
            attemptNumber + 1, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
        return false;
    }

    private async Task<bool> TryProcessPayment(string processorUrl, object paymentData, string processorType, decimal amount)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var json = JsonSerializer.Serialize(paymentData, JsonOptions);
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
