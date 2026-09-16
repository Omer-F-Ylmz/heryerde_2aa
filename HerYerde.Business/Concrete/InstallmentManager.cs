using System.Globalization;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using Microsoft.Extensions.Options;

namespace HerYerde.Business.Concrete;

public class InstallmentManager : IInstallmentService
{
    /// <summary>Taksit tablosu gün içinde değişmez; sağlayıcıya her sayfa açılışında gidilmemesi için bir saat tutulur.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>Ürün sayfasındaki genel tabloda gösterilen taksitler.</summary>
    public static readonly int[] Highlighted = [1, 3, 6, 9];

    private readonly IPaymentProvider _provider;
    private readonly InstallmentCache _cache;
    private readonly IyzicoSettings _iyzico;
    private readonly TimeProvider _clock;

    public InstallmentManager(
        IPaymentProvider provider,
        InstallmentCache cache,
        IOptions<IyzicoSettings> iyzico,
        TimeProvider clock)
    {
        _provider = provider;
        _cache = cache;
        _iyzico = iyzico.Value;
        _clock = clock;
    }

    public bool Enabled => _iyzico.IsConfigured && _iyzico.Installments;

    public async Task<InstallmentTable?> GetAsync(decimal price, string? bin = null, CancellationToken cancellationToken = default)
    {
        if (!Enabled || price <= 0m)
        {
            return null;
        }

        var key = price.ToString("0.00", CultureInfo.InvariantCulture) + "|" + Bin(bin);
        var now = _clock.GetUtcNow().UtcDateTime;
        if (_cache.Get(key, now) is { } cached)
        {
            return cached;
        }

        var result = await _provider.GetInstallmentsAsync(price, Bin(bin), cancellationToken);
        if (!result.Success || Cheapest(result.Tables) is not { } table)
        {
            // Başarısız yanıt önbelleğe girmez: geçici bir ağ hatası tabloyu bir saat boyunca gizlemesin.
            return null;
        }

        _cache.Set(key, table, now, Lifetime);
        return table;
    }

    /// <summary>Genel tablo: öne çıkan taksitlerde en düşük aylık tutarı veren banka; eşitlikte ya da hiçbirinde taksit yoksa
    /// sağlayıcının ilk sırası. BIN'li istekte tek tablo gelir.</summary>
    private static InstallmentTable? Cheapest(IReadOnlyList<InstallmentTable> tables)
        => tables
            .OrderBy(table => table.Options
                .Where(o => o.Count > 1 && Highlighted.Contains(o.Count))
                .Select(o => o.MonthlyPrice)
                .DefaultIfEmpty(decimal.MaxValue)
                .Min())
            .FirstOrDefault();

    /// <summary>Kart numarasının ilk 6 hanesi; eksik ya da rakam dışı karakter varsa BIN yok sayılır.</summary>
    private static string? Bin(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 6 ? digits[..6] : null;
    }
}
