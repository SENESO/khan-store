using System.Text.Json;
using Khan.Core.Orders;
using Paymob.Models;
using Paymob.Webhooks;

namespace Khan.Api;

/// <summary>
/// Builds a realistically-shaped, correctly HMAC-signed Paymob transaction
/// callback for the offline demo checkout. Populates every field Paymob's
/// HMAC concatenation reads, so the signature path the webhook uses in
/// production is the one being exercised here.
/// </summary>
public static class DemoCallback
{
    public static string Build(string merchantOrderId, Order order, bool success, string hmacSecret)
    {
        var data = new TransactionData
        {
            Id = Random.Shared.NextInt64(10_000_000, 99_999_999),
            AmountCents = (int)order.TotalCents,
            Currency = order.Currency,
            Success = success,
            Pending = false,
            ErrorOccured = !success,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),
            HasParentTransaction = false,
            IntegrationId = 1,
            Is3dSecure = true,
            IsAuth = false,
            IsCapture = false,
            IsRefunded = false,
            IsStandalonePayment = true,
            IsVoided = false,
            Owner = 1,
            Order = new CallbackOrder { Id = order.PaymobOrderId ?? 0, MerchantOrderId = merchantOrderId },
            SourceData = new SourceData { Type = "card", Pan = "2346", SubType = "MasterCard" },
        };

        var callback = new TransactionCallback
        {
            Type = "TRANSACTION",
            Data = data,
            Hmac = PaymobWebhookValidator.ComputeHmac(hmacSecret, data),
        };
        return JsonSerializer.Serialize(callback);
    }
}
