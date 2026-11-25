using Khan.Core.Orders;

namespace Khan.Core.Payments;

/// <summary>What the store needs from a payment provider: a page to send the customer to.</summary>
public sealed record PaymentSession(string RedirectUrl, long ProviderOrderId);

public interface IPaymentGateway
{
    Task<PaymentSession> CreateCheckoutAsync(Order order, CancellationToken ct = default);
}
