using Khan.Api;
using Khan.Core.Checkout;
using Khan.Core.Payments;
using Khan.Core.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Khan") ?? "Data Source=data/khan.db";
// SQLite creates the file but not the directory holding it.
string dataSource = connectionString.Split('=', 2)[1].Split(';')[0];
string? dataDir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
if (!string.IsNullOrEmpty(dataDir))
    Directory.CreateDirectory(dataDir);
builder.Services.AddDbContext<KhanDbContext>(o => o.UseSqlite(connectionString));

// Real Paymob when keys are configured, otherwise the offline demo gateway.
bool demoMode = string.IsNullOrWhiteSpace(builder.Configuration["Paymob:ApiKey"]);
builder.Services.AddSingleton<IPaymentGateway>(sp =>
    demoMode ? new FakePaymentGateway() : new PaymobPaymentGateway(builder.Configuration));
builder.Services.AddScoped<CheckoutService>();
string hmacSecret = builder.Configuration["Paymob:HmacSecret"] is { Length: > 0 } s ? s : "dev-hmac-secret";
builder.Services.AddScoped<WebhookProcessor>(_ => new WebhookProcessor(
    _.GetRequiredService<KhanDbContext>(), hmacSecret));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KhanDbContext>();
    db.Database.EnsureCreated();
    if (!db.Products.Any())
    {
        db.Products.AddRange(SeedData.Products);
        db.SaveChanges();
    }
}

app.MapGet("/api/products", async (KhanDbContext db, string? category) =>
    await db.Products
        .Where(p => category == null || p.Category == category)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id, p.Slug, p.Name, p.NameAr, p.Description,
            p.Category, p.PriceCents, p.Currency, p.Stock, p.ImageUrl,
        })
        .ToListAsync());

app.MapPost("/api/orders", async (CheckoutCommand command, CheckoutService checkout, CancellationToken ct) =>
{
    try
    {
        CheckoutResult result = await checkout.PlaceOrderAsync(command, ct);
        return Results.Ok(new
        {
            result.Order.MerchantOrderId,
            result.Order.TotalCents,
            result.Order.Currency,
            result.PaymentRedirectUrl,
        });
    }
    catch (CheckoutException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/orders/{merchantOrderId}", async (string merchantOrderId, KhanDbContext db) =>
{
    var order = await db.Orders.Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.MerchantOrderId == merchantOrderId);
    return order is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            order.MerchantOrderId,
            Status = order.Status.ToString(),
            order.TotalCents,
            order.Currency,
            Items = order.Items.Select(i => new { i.ProductName, i.Quantity, i.UnitPriceCents }),
            order.CreatedAt,
            order.PaidAt,
        });
});

app.MapPost("/api/webhooks/paymob", async (HttpRequest request, WebhookProcessor processor, CancellationToken ct) =>
{
    string json = await new StreamReader(request.Body).ReadToEndAsync(ct);
    WebhookOutcome outcome = await processor.ProcessAsync(json, ct);
    return outcome switch
    {
        WebhookOutcome.InvalidHmac => Results.Unauthorized(),
        WebhookOutcome.UnknownOrder => Results.NotFound(new { error = "unknown order" }),
        _ => Results.Ok(new { outcome = outcome.ToString() }),
    };
});

// Demo checkout only: pays a pending order by signing a callback with the dev
// secret and feeding it through the real webhook path. Never registered when
// real Paymob keys are present.
if (demoMode)
{
    app.MapPost("/api/dev/demo-pay", async (DemoPayRequest request, WebhookProcessor processor, KhanDbContext db, IConfiguration config, CancellationToken ct) =>
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.MerchantOrderId == request.MerchantOrderId, ct);
        if (order is null)
            return Results.NotFound();

        string json = DemoCallback.Build(request.MerchantOrderId, order, request.Success, hmacSecret);
        WebhookOutcome outcome = await processor.ProcessAsync(json, ct);
        return Results.Ok(new { outcome = outcome.ToString() });
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

public sealed record DemoPayRequest(string MerchantOrderId, bool Success);

// WebApplicationFactory in the test project hooks this.
public partial class Program;
