namespace Khan.Core.Catalog;

/// <summary>A sellable item. Prices live here and nowhere else - the server
/// always re-prices a cart from the database, never from client input.</summary>
public sealed class Product
{
    public int Id { get; set; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string NameAr { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public long PriceCents { get; set; }
    public string Currency { get; set; } = "EGP";
    public int Stock { get; set; }
    public string? ImageUrl { get; set; }
}
