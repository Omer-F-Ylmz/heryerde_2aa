using HerYerde.Business.Rules;

namespace HerYerde.Tests.Business;

/// <summary>D11 B2: doğrulayıcı uygulamalarla aynı kodu üretmeli; kendi içinde tutarlı ama standart dışı bir TOTP
/// web testlerinden geçer, gerçek telefonda geçmez.</summary>
public sealed class TotpTests
{
    /// <summary>RFC 6238 Ek B, SHA-1 anahtarı "12345678901234567890"; 8 hanelik değerlerin son 6 hanesi.</summary>
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(2000000000L, "279037")]
    public void Rfc6238_test_vektorleriyle_ayni_kodu_uretir(long unixSeconds, string expected)
    {
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

        Assert.Equal(expected, Totp.Code(secret, DateTimeOffset.FromUnixTimeSeconds(unixSeconds)));
    }

    [Fact]
    public void Bir_adim_kayma_kabul_iki_adim_reddedilir()
    {
        var secret = Totp.NewSecret();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        Assert.True(Totp.Verify(secret, Totp.Code(secret, now.AddSeconds(-30)), now));
        Assert.False(Totp.Verify(secret, Totp.Code(secret, now.AddSeconds(-90)), now));
    }
}
