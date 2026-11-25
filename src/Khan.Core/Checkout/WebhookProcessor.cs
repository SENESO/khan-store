using System.Text.Json;
using Khan.Core.Orders;
using Khan.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Paymob.Models;
using Paymob.Webhooks;
using OrderItem = Khan.Core.Orders.OrderItem;

namespace Khan.Core.Checkout;

public enum WebhookOutcome
{
    InvalidHmac,
    UnknownOrder,
    OrderPaid,
    OrderFailed,
    Duplicate,
    StillPending,
}

/// <summary>
/// Handles Paymob's transaction callback: validates the HMAC signature before
/// trusting a single byte, then moves the order. Idempotent - Paymob retries
/// callbacks, so a repeat delivery of a paid order is a no-op, not an error.
/// </summary>
public sealed class WebhookProcessor
{
    private readonly KhanDbContext _db;
    private readonly string _hmacSecret;

    public WebhookProcessor(KhanDbContext db, string hmacSecret)
    {
        _db = db;
        _hmacSecret = hmacSecret;
    }

    public async Task<WebhookOutcome> ProcessAsync(string callbackJson, CancellationToken ct = default)
    {
        if (!PaymobWebhookValidator.IsValidCallback(_hmacSecret, callbackJson))
            return WebhookOutcome.InvalidHmac;

        var callback = JsonSerializer.Deserialize<TransactionCallback>(callbackJson);
        string? merchantOrderId = callback?.Data?.Order?.MerchantOrderId;
        if (merchantOrderId is null)
            return WebhookOutcome.UnknownOrder;

        Order? order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.MerchantOrderId == merchantOrderId, ct);
        if (order is null)
            return WebhookOutcome.UnknownOrder;

        if (order.Status == OrderStatus.Paid)
            return WebhookOutcome.Duplicate;

        TransactionData txn = callback!.Data!;
        order.PaymobTransactionId = txn.Id;

        if (txn.Pending)
            return WebhookOutcome.StillPending;

        if (txn.Success && !txn.ErrorOccured)
        {
            order.Status = OrderStatus.Paid;
            order.PaidAt = DateTimeOffset.UtcNow;
            foreach (OrderItem item in order.Items)
            {
                // Decrement only now; pending carts never held stock.
                await _db.Database.ExecuteSqlAsync(
                    $"UPDATE Products SET Stock = Stock - {item.Quantity} WHERE Id = {item.ProductId} AND Stock >= {item.Quantity}", ct);
            }
            await _db.SaveChangesAsync(ct);
            return WebhookOutcome.OrderPaid;
        }

        order.Status = OrderStatus.Failed;
        await _db.SaveChangesAsync(ct);
        return WebhookOutcome.OrderFailed;
    }
}
