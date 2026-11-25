using Khan.Core.Orders;
using Khan.Core.Payments;
using Khan.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Khan.Core.Checkout;

public sealed record CheckoutLine(int ProductId, int Quantity);

public sealed record CheckoutCommand(
    string CustomerName,
    string Email,
    string Phone,
    string City,
    IReadOnlyList<CheckoutLine> Lines);

public sealed record CheckoutResult(Order Order, string PaymentRedirectUrl);

/// <summary>
/// Turns a cart into a paid-or-pending order. The pricing rule that matters:
/// only product ids and quantities cross the wire - prices are read back from
/// the catalog here, so a browser can never name its own price.
/// </summary>
public sealed class CheckoutService
{
    private readonly KhanDbContext _db;
    private readonly IPaymentGateway _gateway;

    public CheckoutService(KhanDbContext db, IPaymentGateway gateway)
    {
        _db = db;
        _gateway = gateway;
    }

    public async Task<CheckoutResult> PlaceOrderAsync(CheckoutCommand command, CancellationToken ct = default)
    {
        if (command.Lines.Count == 0)
            throw new CheckoutException("The cart is empty.");
        if (command.Lines.Any(l => l.Quantity <= 0))
            throw new CheckoutException("Quantities must be positive.");

        var ids = command.Lines.Select(l => l.ProductId).ToArray();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var missing = ids.Where(id => !products.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
            throw new CheckoutException($"Unknown product(s): {string.Join(", ", missing)}.");

        var order = new Order
        {
            MerchantOrderId = $"khan-{Guid.NewGuid():N}"[..12],
            CustomerName = command.CustomerName.Trim(),
            Email = command.Email.Trim(),
            Phone = command.Phone.Trim(),
            City = command.City.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        foreach (CheckoutLine line in command.Lines)
        {
            var product = products[line.ProductId];
            if (product.Stock < line.Quantity)
                throw new CheckoutException($"'{product.Name}' has {product.Stock} left, not {line.Quantity}.");

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPriceCents = product.PriceCents,
                Quantity = line.Quantity,
            });
        }
        order.TotalCents = order.Items.Sum(i => i.LineTotalCents);

        // Stock moves only after a successful payment (see the webhook), so a
        // cart abandoned at the Paymob page never holds inventory.
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        PaymentSession session = await _gateway.CreateCheckoutAsync(order, ct);
        order.PaymobOrderId = session.ProviderOrderId;
        await _db.SaveChangesAsync(ct);

        return new CheckoutResult(order, session.RedirectUrl);
    }
}

public sealed class CheckoutException : Exception
{
    public CheckoutException(string message) : base(message) { }
}
