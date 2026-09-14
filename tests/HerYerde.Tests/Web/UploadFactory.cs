using System.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace HerYerde.Tests.Web;

/// <summary>Yüklenen dosyalar depoya değil, teste özel geçici klasöre yazılır.</summary>
public sealed class UploadFactory : AdminWebFactory
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "heryerde-yukleme-" + Guid.NewGuid().ToString("n"));

    protected override void Configure(Dictionary<string, string?> settings)
        => settings["Uploads:Root"] = Root;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}

public static class UploadForm
{
    /// <summary>Çok parçalı yükleme; dosya adı ve içerik türü çağrandan gelir ki sahte değerler denenebilsin.</summary>
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        int productId,
        params (string FileName, byte[] Bytes)[] files)
    {
        var token = await HtmlForm.AntiforgeryTokenAsync(client, $"/admin/products/edit/{productId}");
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new StringContent(productId.ToString()), "ProductId" },
            { new StringContent("Ürün görseli"), "Alt" },
            { new StringContent("0"), "SortOrder" }
        };

        foreach (var (fileName, bytes) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(part, "Files", fileName);
        }

        return await client.PostAsync("/admin/products/addimage", content);
    }
}

public static class TestImage
{
    /// <summary>Tek renk png; kare doldurma testinde oranı ölçmek için en/boy ayrı verilir.</summary>
    public static byte[] Png(int width, int height, byte red = 220, byte green = 30, byte blue = 30)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(red, green, blue));
        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());
        return buffer.ToArray();
    }

    /// <summary>Kenarı ve göbeği ayrı renkte png; kare dolgu rengi kenardan okunduğu için ikisi ayrışmalı.</summary>
    public static byte[] Framed(int width, int height, Rgba32 border, Rgba32 center)
    {
        using var image = new Image<Rgba32>(width, height, border);
        for (var y = height / 5; y < height - (height / 5); y++)
        {
            for (var x = width / 5; x < width - (width / 5); x++)
            {
                image[x, y] = center;
            }
        }

        using var buffer = new MemoryStream();
        image.Save(buffer, new PngEncoder());
        return buffer.ToArray();
    }
}
