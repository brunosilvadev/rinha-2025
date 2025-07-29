using Rinha.Models;
using Rinha.Services;

namespace Rinha.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/payments", async (PaymentRequest request, PaymentService paymentService) =>
        {
            if (request.CorrelationId == Guid.Empty)
            {
                return Results.BadRequest("CorrelationId is required and cannot be empty.");
            }

            if (request.Amount <= 0)
            {
                return Results.BadRequest("Amount must be greater than zero.");
            }

            var success = await paymentService.ProcessPaymentAsync(request);
            
            if (success)
            {
                return Results.Ok(new { message = "Payment processed successfully", correlationId = request.CorrelationId });
            }
            else
            {
                return Results.Problem("Failed to process payment", statusCode: 500);
            }
        });

        app.MapGet("/payments-summary", async (string? from, string? to, PaymentSummaryService summaryService) =>
        {
            // Validate query parameters
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                return Results.BadRequest("Both 'from' and 'to' query parameters are required.");
            }

            if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
            {
                return Results.BadRequest("Invalid date format. Please use UTC ISO format (e.g., 2025-01-01T00:00:00Z).");
            }

            if (fromDate > toDate)
            {
                return Results.BadRequest("'from' date cannot be greater than 'to' date.");
            }

            // Get actual data from Redis
            var summary = await summaryService.GetSummaryAsync(fromDate, toDate);
            return Results.Ok(summary);
        });

        // Optional: Reset endpoint for testing
        app.MapDelete("/payments-summary", async (PaymentSummaryService summaryService) =>
        {
            await summaryService.ResetSummaryAsync();
            return Results.Ok(new { message = "Payment summary reset successfully" });
        });
    }
}
