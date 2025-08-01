using Rinha.Models;
using Rinha.Services;

namespace Rinha.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/payments", async (PaymentRequest request, PaymentService paymentService) =>
        {
            // Minimal validation for speed - trust the input more
            if (request.CorrelationId == Guid.Empty || request.Amount <= 0)
            {
                return Results.BadRequest();
            }

            var success = await paymentService.ProcessPaymentAsync(request);
            
            return success ? Results.Ok() : Results.Problem(statusCode: 500);
        });

        app.MapGet("/payments-summary", async (string? from, string? to, PaymentSummaryService summaryService) =>
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;

            // Parse optional date parameters
            if (!string.IsNullOrEmpty(from) && DateTime.TryParse(from, out var parsedFrom))
            {
                fromDate = parsedFrom;
            }
            
            if (!string.IsNullOrEmpty(to) && DateTime.TryParse(to, out var parsedTo))
            {
                toDate = parsedTo;
            }

            // Validate date range if both are provided
            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            {
                return Results.BadRequest();
            }

            var summary = await summaryService.GetSummaryAsync(fromDate, toDate);
            return Results.Ok(summary);
        });

        // Optional: Reset endpoint for testing
        app.MapDelete("/payments-summary", async (PaymentSummaryService summaryService) =>
        {
            await summaryService.ResetSummaryAsync();
            return Results.Ok(new { message = "Payment summary reset successfully" });
        });

        // Test endpoint for health check service
        app.MapGet("/test/health-check", async (PaymentHealthCheckService healthCheckService) =>
        {
            var defaultHealth = await healthCheckService.GetDefaultProcessorHealthAsync();
            var fallbackHealth = await healthCheckService.GetFallbackProcessorHealthAsync();

            return Results.Ok(new
            {
                @default = defaultHealth != null ? new
                {
                    failing = defaultHealth.Failing,
                    minResponseTime = defaultHealth.MinResponseTime
                } : null,
                fallback = fallbackHealth != null ? new
                {
                    failing = fallbackHealth.Failing,
                    minResponseTime = fallbackHealth.MinResponseTime
                } : null
            });
        });
    }
}
