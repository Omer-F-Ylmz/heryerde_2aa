using HerYerde.Business.Rules;
using HerYerde.Entities.Enums;

namespace HerYerde.Tests.Business;

public sealed class OrderRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Beklemede, OrderStatus.Onaylandi)]
    [InlineData(OrderStatus.Onaylandi, OrderStatus.Kargoda)]
    [InlineData(OrderStatus.Kargoda, OrderStatus.TeslimEdildi)]
    public void Durum_bir_sonraki_asamaya_gecebilir(OrderStatus from, OrderStatus to)
        => Assert.True(OrderRules.CanTransition(from, to));

    [Theory]
    [InlineData(OrderStatus.Onaylandi, OrderStatus.Beklemede)]
    [InlineData(OrderStatus.Kargoda, OrderStatus.Onaylandi)]
    [InlineData(OrderStatus.TeslimEdildi, OrderStatus.Kargoda)]
    [InlineData(OrderStatus.Beklemede, OrderStatus.Kargoda)]
    public void Geri_veya_asama_atlayan_gecis_reddedilir(OrderStatus from, OrderStatus to)
        => Assert.False(OrderRules.CanTransition(from, to));

    [Fact]
    public void Iptal_yalniz_beklemede_yapilir()
    {
        Assert.True(OrderRules.CanTransition(OrderStatus.Beklemede, OrderStatus.IptalEdildi));
        Assert.False(OrderRules.CanTransition(OrderStatus.Onaylandi, OrderStatus.IptalEdildi));
        Assert.False(OrderRules.CanTransition(OrderStatus.Kargoda, OrderStatus.IptalEdildi));
        Assert.False(OrderRules.CanTransition(OrderStatus.TeslimEdildi, OrderStatus.IptalEdildi));
    }

    [Fact]
    public void Iptal_edilen_siparis_hicbir_duruma_donmez()
    {
        Assert.False(OrderRules.CanTransition(OrderStatus.IptalEdildi, OrderStatus.Beklemede));
        Assert.False(OrderRules.CanTransition(OrderStatus.IptalEdildi, OrderStatus.Onaylandi));
    }
}

public sealed class OrderNoTests
{
    [Fact]
    public void Numara_HY_tarih_sira_bicimindedir()
        => Assert.Equal("HY-20260910-0001", OrderNo.Build(new DateTime(2026, 9, 10), 1));

    [Fact]
    public void Sira_dort_haneye_tamamlanir()
        => Assert.Equal("HY-20260910-0042", OrderNo.Build(new DateTime(2026, 9, 10), 42));

    [Fact]
    public void Sira_dort_haneyi_asinca_kirpilmaz()
        => Assert.Equal("HY-20260910-12345", OrderNo.Build(new DateTime(2026, 9, 10), 12345));

    [Fact]
    public void Sira_genel_oldugu_icin_tarih_degisince_sifirlanmaz()
        => Assert.Equal("HY-20260911-0007", OrderNo.Build(new DateTime(2026, 9, 11), 7));

    [Fact]
    public void Gunun_onekinden_o_gune_ait_numaralar_ayirt_edilir()
        => Assert.Equal("HY-20260910-", OrderNo.PrefixFor(new DateTime(2026, 9, 10, 23, 59, 0)));
}

public sealed class PhoneRulesTests
{
    [Theory]
    [InlineData("05424970982", "05424970982")]
    [InlineData("5424970982", "05424970982")]
    [InlineData("+90 542 497 09 82", "05424970982")]
    [InlineData("0542 497 09 82", "05424970982")]
    public void Gecerli_telefon_tek_bicime_indirgenir(string input, string expected)
    {
        Assert.True(PhoneRules.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0542497098")]
    [InlineData("054249709821")]
    [InlineData("02124970982")]
    [InlineData("telefon yok")]
    public void Gecersiz_telefon_reddedilir(string input)
        => Assert.False(PhoneRules.TryNormalize(input, out _));
}
