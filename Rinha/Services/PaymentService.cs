using System.Text;
using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentService(HttpClient httpClient, ILogger<PaymentService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<PaymentService> _logger = logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<bool> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        try
        {
            var json = JsonSerializer.Serialize(paymentRequest, _jsonOptions);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("myApi/Payments", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Payment processed successfully for correlation ID: {CorrelationId}",
                    paymentRequest.CorrelationId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to process payment for correlation ID: {CorrelationId}. Status: {StatusCode}",
                    paymentRequest.CorrelationId, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex) //TODO: final submission will not have try-catch
        {
            _logger.LogError(ex, "Error processing payment for correlation ID: {CorrelationId}",
                paymentRequest.CorrelationId);
            return false;
        }
    }
}
