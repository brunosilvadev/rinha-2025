namespace Rinha.Models;

public class SummaryResponse
{
    public PaymentProcessorSummary Default { get; set; } = new();
    public PaymentProcessorSummary Fallback { get; set; } = new();
}
