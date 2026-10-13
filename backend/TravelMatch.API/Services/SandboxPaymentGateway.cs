namespace TravelMatch.API.Services;

public class PaymentSimulationResult
{
    public bool IsSuccess { get; set; }

    public string TransactionReference { get; set; } = string.Empty;

    public string? FailureReason { get; set; }
}

public interface ISandboxPaymentGateway
{
    Task<PaymentSimulationResult> ProcessPaymentAsync(decimal amount, string currency, string? paymentMethod = null);
}

public class SandboxPaymentGateway : ISandboxPaymentGateway
{
    public Task<PaymentSimulationResult> ProcessPaymentAsync(decimal amount, string currency, string? paymentMethod = null)
    {
        // Sandbox payment simulation:
        // By default succeeds with simulated transaction reference.
        // If paymentMethod is specified as "FAIL" or "DECLINED" (or negative amount), simulates a gateway decline.
        // This keeps simulation decoupled from domain entities and realistic for sandbox testing.
        if (string.Equals(paymentMethod, "FAIL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(paymentMethod, "DECLINED", StringComparison.OrdinalIgnoreCase) ||
            amount <= 0)
        {
            return Task.FromResult(new PaymentSimulationResult
            {
                IsSuccess = false,
                TransactionReference = $"SIM-FAIL-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                FailureReason = "Payment declined by sandbox payment processor."
            });
        }

        return Task.FromResult(new PaymentSimulationResult
        {
            IsSuccess = true,
            TransactionReference = $"SIM-TXN-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            FailureReason = null
        });
    }
}
