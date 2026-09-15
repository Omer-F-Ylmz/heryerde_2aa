using System.Net;

namespace HerYerde.Tests.Web;

/// <summary>KAPANIŞ-3 ZAP: onay kutusuna bool olmayan değer gönderilince form yeniden çizilirken InputTagHelper
/// FormatException atıyor, yönetim 500 dönüyordu; bozuk değer doğrulama hatası olur, form yeniden çizilir.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CheckboxBindingTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("/admin/categories/create", "IsActive")]
    [InlineData("/admin/products/create", "IsActive")]
    [InlineData("/admin/orders/new", "NotifyCustomer")]
    public async Task Onay_kutusuna_bool_olmayan_deger_500_vermez_form_yeniden_cizilir(string path, string field)
    {
        var admin = await _factory.CreateSignedInClientAsync();

        var response = await HtmlForm.PostAsync(admin, path, path, new Dictionary<string, string> { [field] = "zap" });

        // Geçersiz form her denetleyicinin kendi kuralıyla döner (ürün/kategori 200, manuel sipariş 400); sunucu hatası yok.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
        Assert.Contains($"name=\"{field}\"", await response.Content.ReadAsStringAsync());
    }
}
