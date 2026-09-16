using System.Globalization;
using System.Net;
using HerYerde.DataAccess.Concrete.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace HerYerde.Tests.Web;

/// <summary>D15 B2: ürün özellikleri ve kategori özellik şablonu; ürün sayfasında tablo, JSON-LD'de additionalProperty.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductAttributeTests : IAsyncLifetime
{
    private readonly AdminWebFactory _factory = new();

    public Task InitializeAsync() => TestDb.ResetAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Urun_sayfasi_ozellikler_tablosunu_markayi_ve_json_ld_yazar()
    {
        await using (var context = TestDb.NewContext())
        {
            var productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            await TestData.SetBrandAsync(context, productId, await TestData.AddBrandAsync(context, "TAÇ", "tac"));
            await TestData.AddAttributeAsync(context, productId, "Hacim", "3 L", sortOrder: 1);
            await TestData.AddAttributeAsync(context, productId, "Malzeme", "Çelik", sortOrder: 0);
        }

        var html = await (await _factory.CreateNonRedirectingClient().GetAsync("/urun/celik-tencere")).Content.ReadAsStringAsync();

        Assert.Contains("Özellikler", html);
        Assert.True(html.IndexOf("Malzeme", StringComparison.Ordinal) < html.IndexOf("Hacim", StringComparison.Ordinal));
        Assert.Contains("<a href=\"/marka/tac\">TAÇ</a>", html);
        Assert.Contains("\"brand\":{\"@type\":\"Brand\",\"name\":\"TAÇ\"}", html);
        Assert.Contains("\"additionalProperty\":[{\"@type\":\"PropertyValue\",\"name\":\"Malzeme\",\"value\":\"Çelik\"},{\"@type\":\"PropertyValue\",\"name\":\"Hacim\",\"value\":\"3 L\"}]", html);
    }

    [Fact]
    public async Task Yonetimden_ozellikler_satir_satir_kaydedilir_hatali_satir_hicbirini_yazmaz()
    {
        int productId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
        }

        var client = await _factory.CreateSignedInClientAsync();
        var saved = await PostAttributesAsync(client, productId, "Malzeme: Çelik\nHacim: 3 L");

        Assert.Equal(HttpStatusCode.Found, saved.StatusCode);
        await using (var context = TestDb.NewContext())
        {
            Assert.Equal(
                [("Malzeme", "Çelik"), ("Hacim", "3 L")],
                (await context.ProductAttributes.OrderBy(a => a.SortOrder).ToListAsync()).Select(a => (a.Name, a.Value)));
        }

        var rejected = await PostAttributesAsync(client, productId, "Malzeme Döküm");

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        await using var check = TestDb.NewContext();
        Assert.Equal(2, await check.ProductAttributes.CountAsync());
    }

    [Fact]
    public async Task Kategori_sablonu_urun_formunda_bos_satir_olarak_onerilir()
    {
        int productId;
        int categoryId;
        await using (var context = TestDb.NewContext())
        {
            productId = await TestData.AddHomeProductAsync(context, "Çelik Tencere", "celik-tencere");
            categoryId = (await new EfProductDal(context).GetAsync(p => p.Id == productId))!.CategoryId;
        }

        var client = await _factory.CreateSignedInClientAsync();
        var saved = await HtmlForm.PostAsync(client, $"/admin/categories/edit/{categoryId}", "/admin/categories/edit", new Dictionary<string, string>
        {
            ["Id"] = categoryId.ToString(CultureInfo.InvariantCulture),
            ["Name"] = "Ev",
            ["SortOrder"] = "1",
            ["IsActive"] = "true",
            ["IsRoot"] = "true",
            ["AttributeTemplate"] = "Malzeme\nHacim"
        });

        Assert.Equal(HttpStatusCode.Found, saved.StatusCode);
        await using (var context = TestDb.NewContext())
        {
            Assert.Equal(["Malzeme", "Hacim"], (await context.CategoryAttributeTemplates.OrderBy(t => t.SortOrder).ToListAsync()).Select(t => t.Name));
        }

        var form = await (await client.GetAsync($"/admin/products/edit/{productId}")).Content.ReadAsStringAsync();
        // Satır sonu metin alanında &#xA; olarak kodlanır; tarayıcı çözer.
        Assert.Contains("Malzeme: \nHacim: ", WebUtility.HtmlDecode(form));
    }

    private static Task<HttpResponseMessage> PostAttributesAsync(HttpClient client, int productId, string text)
        => HtmlForm.PostAsync(client, $"/admin/products/edit/{productId}", "/admin/products/attributes", new Dictionary<string, string>
        {
            ["productId"] = productId.ToString(CultureInfo.InvariantCulture),
            ["text"] = text
        });
}
