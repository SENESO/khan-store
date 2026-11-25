using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Khan.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Khan.Tests;

/// <summary>
/// Full-stack API tests against the real app with a throwaway SQLite file.
/// Demo mode is active (no Paymob keys), so the payment leg goes through the
/// fake gateway and the signed-callback demo endpoint - the same webhook code
/// production runs.
/// </summary>
public sealed class StoreApiTests : IClassFixture<StoreApiTests.Factory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public StoreApiTests(Factory factory) => _client = factory.CreateClient();

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"khan-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Khan"] = $"Data Source={_dbPath}",
            }));
        }
    }

    private record OrderCreated(string MerchantOrderId, long TotalCents, string Currency, string PaymentRedirectUrl);

    private async Task<OrderCreated> PlaceOrder(int productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "Test Buyer",
            email = "buyer@example.com",
            phone = "+201000000000",
            city = "Cairo",
            lines = new[] { new { productId, quantity } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderCreated>(Json))!;
    }

    [Fact]
    public async Task Catalog_IsSeeded()
    {
        var products = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        Assert.NotNull(products);
        Assert.True(products!.Length >= 8);
        Assert.Contains(products, p => p.GetProperty("slug").GetString() == "koshari-kit");
    }

    [Fact]
    public async Task Checkout_TotalComesFromServerSidePrices()
    {
        // The client sends only product id + quantity; pricing is the server's job.
        var products = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        var koshari = products!.First(p => p.GetProperty("slug").GetString() == "koshari-kit");
        long price = koshari.GetProperty("priceCents").GetInt64();
        int id = koshari.GetProperty("id").GetInt32();

        OrderCreated order = await PlaceOrder(id, 3);

        Assert.Equal(price * 3, order.TotalCents);
        Assert.Equal("EGP", order.Currency);
        Assert.StartsWith("/pay/demo", order.PaymentRedirectUrl);
    }

    [Fact]
    public async Task Checkout_RejectsUnknownProduct_AndZeroQuantity()
    {
        var unknown = await _client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "X", email = "x@example.com", phone = "1", city = "Cairo",
            lines = new[] { new { productId = 99999, quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var zero = await _client.PostAsJsonAsync("/api/orders", new
        {
            customerName = "X", email = "x@example.com", phone = "1", city = "Cairo",
            lines = new[] { new { productId = 1, quantity = 0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
    }

    [Fact]
    public async Task DemoPayment_MarksOrderPaid_AndDecrementsStock()
    {
        var products = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        var koshari = products!.First(p => p.GetProperty("slug").GetString() == "koshari-kit");
        int stockBefore = koshari.GetProperty("stock").GetInt32();

        OrderCreated order = await PlaceOrder(koshari.GetProperty("id").GetInt32(), 2);

        var pay = await _client.PostAsJsonAsync("/api/dev/demo-pay", new { merchantOrderId = order.MerchantOrderId, success = true });
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);

        var status = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{order.MerchantOrderId}");
        Assert.Equal("Paid", status.GetProperty("status").GetString());

        var after = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        int stockAfter = after!.First(p => p.GetProperty("slug").GetString() == "koshari-kit").GetProperty("stock").GetInt32();
        Assert.Equal(stockBefore - 2, stockAfter);
    }

    [Fact]
    public async Task FailedPayment_MarksOrderFailed_AndKeepsStock()
    {
        var products = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        var honey = products!.First(p => p.GetProperty("slug").GetString() == "sinai-honey");
        int stockBefore = honey.GetProperty("stock").GetInt32();

        OrderCreated order = await PlaceOrder(honey.GetProperty("id").GetInt32(), 1);
        await _client.PostAsJsonAsync("/api/dev/demo-pay", new { merchantOrderId = order.MerchantOrderId, success = false });

        var status = await _client.GetFromJsonAsync<JsonElement>($"/api/orders/{order.MerchantOrderId}");
        Assert.Equal("Failed", status.GetProperty("status").GetString());

        var after = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        Assert.Equal(stockBefore, after!.First(p => p.GetProperty("slug").GetString() == "sinai-honey").GetProperty("stock").GetInt32());
    }

    [Fact]
    public async Task Webhook_RejectsForgedSignature()
    {
        string forged = """{"type":"TRANSACTION","hmac":"deadbeef","obj":{"id":1,"success":true,"order":{"merchantOrderId":"khan-0000"}}}""";
        var response = await _client.PostAsync("/api/webhooks/paymob", new StringContent(forged, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatePaidCallback_IsNoOp_StockDecrementedOnce()
    {
        var products = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        var tawla = products!.First(p => p.GetProperty("slug").GetString() == "tawla-board");
        int stockBefore = tawla.GetProperty("stock").GetInt32();

        OrderCreated order = await PlaceOrder(tawla.GetProperty("id").GetInt32(), 1);
        await _client.PostAsJsonAsync("/api/dev/demo-pay", new { merchantOrderId = order.MerchantOrderId, success = true });
        var second = await _client.PostAsJsonAsync("/api/dev/demo-pay", new { merchantOrderId = order.MerchantOrderId, success = true });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var after = await _client.GetFromJsonAsync<JsonElement[]>("/api/products");
        int stockAfter = after!.First(p => p.GetProperty("slug").GetString() == "tawla-board").GetProperty("stock").GetInt32();
        Assert.Equal(stockBefore - 1, stockAfter);
    }
}
