using Khan.Core.Orders;
using Microsoft.Extensions.Configuration;
using Paymob;
using Paymob.Models;

namespace Khan.Core.Payments;

/// <summary>
/// Real gateway: drives a Paymob classic checkout (auth -> order -> payment
/// key -> iframe URL) through the SENESO.Paymob.Net SDK.
/// </summary>
public sealed class PaymobPaymentGateway : IPaymentGateway
{
    private readonly PaymobClient _client;
    private readonly int _integrationId;
    private readonly int _iframeId;

    public PaymobPaymentGateway(IConfiguration config)
    {
        var section = config.GetSection("Paymob");
        _client = new PaymobClient(new PaymobClientOptions
        {
            ApiKey = section["ApiKey"] ?? throw new InvalidOperationException("Paymob:ApiKey is required."),
            SecretKey = section["SecretKey"],
            HmacSecret = section["HmacSecret"],
        });
        _integrationId = int.Parse(section["IntegrationId"] ?? "0");
        _iframeId = int.Parse(section["IframeId"] ?? "0");
    }

    public async Task<PaymentSession> CreateCheckoutAsync(Order order, CancellationToken ct = default)
    {
        var nameParts = order.CustomerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        CheckoutResult checkout = await _client.CreateCheckoutAsync(new CheckoutRequest
        {
            AmountCents = checked((int)order.TotalCents),
            Currency = order.Currency,
            MerchantOrderId = order.MerchantOrderId,
            IntegrationId = _integrationId,
            IframeId = _iframeId,
            BillingData = new BillingData
            {
                FirstName = nameParts.FirstOrDefault() ?? order.CustomerName,
                LastName = nameParts.Skip(1).FirstOrDefault() ?? "-",
                Email = order.Email,
                PhoneNumber = order.Phone,
                City = order.City,
                Country = "EG",
            },
        });
        return new PaymentSession(checkout.IframeUrl, checkout.PaymobOrderId);
    }
}
