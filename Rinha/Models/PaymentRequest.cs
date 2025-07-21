namespace Rinha.Models;

public class PaymentRequest
{
    public Guid CorrelationId { get; set; }
    public decimal Amount { get; set; }
}
