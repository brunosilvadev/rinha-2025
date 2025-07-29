namespace Rinha.Models;

public class PaymentProcessorHealthCheck
{
    public bool Failing { get; set; }
    public int MinResponseTime { get; set; }
}
