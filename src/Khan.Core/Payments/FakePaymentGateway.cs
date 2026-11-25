using Khan.Core.Orders;

namespace Khan.Core.Payments;

/// <summary>
/// Demo gateway used when no Paymob keys are configured: no network, no real
/// money. The checkout URL points at the app's own mock payment page, which
/// can then fire a properly signed test webhook - the whole flow stays
/// exercisable end to end without a Paymob account.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private long _nextProviderOrderId = 100000;

    public Task<PaymentSession> CreateCheckoutAsync(Order order, CancellationToken ct = default)
    {
        long providerOrderId = Interlocked.Increment(ref _nextProviderOrderId);
        string redirectUrl = $"/pay/demo?order={Uri.EscapeDataString(order.MerchantOrderId)}&amount={order.TotalCents}";
        return Task.FromResult(new PaymentSession(redirectUrl, providerOrderId));
    }
}
