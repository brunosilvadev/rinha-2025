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

        app.MapGet("/payments-summary", (string? from, string? to) =>
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

            // TODO: Replace with actual data retrieval logic
            var summary = new SummaryResponse
            {
                Default = new PaymentProcessorSummary
                {
                    TotalRequests = 150,
                    TotalAmount = 15000.50m
                },
                Fallback = new PaymentProcessorSummary
                {
                    TotalRequests = 25,
                    TotalAmount = 2500.75m
                }
            };

            return Results.Ok(summary);
        });
    }
}
