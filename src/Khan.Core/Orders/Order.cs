namespace Khan.Core.Orders;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3,
}

public sealed class Order
{
    public int Id { get; set; }

    /// <summary>The id Paymob calls back with, e.g. "khan-9f3ac21b".</summary>
    public required string MerchantOrderId { get; set; }

    public required string CustomerName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public required string City { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public long TotalCents { get; set; }
    public string Currency { get; set; } = "EGP";

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public long? PaymobOrderId { get; set; }
    public long? PaymobTransactionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    /// <summary>Snapshot at purchase time - later price edits never rewrite history.</summary>
    public required string ProductName { get; set; }
    public long UnitPriceCents { get; set; }
    public int Quantity { get; set; }

    public long LineTotalCents => UnitPriceCents * Quantity;
}
