using System.Globalization;
using HerYerde.Business.Abstract;
using HerYerde.Business.Dtos;
using HerYerde.Business.Rules;
using HerYerde.Web.Areas.Admin.Models;
using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Satış raporu: tarih aralığı İstanbul günleriyle, bitiş günü dahil; varsayılan son 30 gün.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class ReportController(IReportService reportService, TimeProvider clock) : Controller
{
    [HttpGet("admin/rapor")]
    public async Task<IActionResult> Index(DateOnly? baslangic, DateOnly? bitis, string? donem, CancellationToken cancellationToken)
        => View(await BuildAsync(baslangic, bitis, donem, cancellationToken));

    [HttpGet("admin/rapor/csv")]
    public async Task<IActionResult> Csv(DateOnly? baslangic, DateOnly? bitis, string? donem, CancellationToken cancellationToken)
    {
        var model = await BuildAsync(baslangic, bitis, donem, cancellationToken);
        var report = model.Report;
        List<IReadOnlyList<string>> rows =
        [
            ["Aralık", $"{model.From:dd.MM.yyyy} - {model.To:dd.MM.yyyy}"],
            ["Ciro", CsvFile.Money(report.Revenue)],
            ["Sipariş", Count(report.OrderCount)],
            ["Ödenmiş sipariş", Count(report.PaidOrderCount)],
            ["Ortalama sepet", CsvFile.Money(report.AverageBasket)],
            ["İptal oranı", (report.CancelRate * 100).ToString("0.0", CultureInfo.GetCultureInfo("tr-TR")) + " %"],
            [],
            ["Dönem başı", "Ciro", "Sipariş"]
        ];
        rows.AddRange(report.Periods.Select(p => (IReadOnlyList<string>)[p.Start.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), CsvFile.Money(p.Revenue), Count(p.Orders)]));
        rows.Add([]);
        rows.Add(["Çok satan", "Stok kodu", "Adet", "Tutar"]);
        rows.AddRange(report.TopProducts.Select(p => (IReadOnlyList<string>)[p.ProductName, p.Sku, Count(p.Quantity), CsvFile.Money(p.Amount)]));
        rows.Add([]);
        rows.Add(["Kanal", "Sipariş", "Ciro"]);
        rows.AddRange(report.Sources.Select(s => (IReadOnlyList<string>)[PaymentLabels.Source(s.Source), Count(s.Orders), CsvFile.Money(s.Revenue)]));
        rows.Add([]);
        rows.Add(["Ödeme yöntemi", "Sipariş", "Ciro"]);
        rows.AddRange(report.PaymentMethods.Select(m => (IReadOnlyList<string>)[PaymentLabels.Method(m.Method), Count(m.Orders), CsvFile.Money(m.Revenue)]));

        return File(CsvFile.Build(rows), CsvFile.ContentType, $"rapor-{model.From:yyyyMMdd}-{model.To:yyyyMMdd}.csv");
    }

    private async Task<ReportViewModel> BuildAsync(DateOnly? from, DateOnly? to, string? period, CancellationToken cancellationToken)
    {
        var end = to ?? IstanbulTime.Today(clock);
        var start = from ?? end.AddDays(-29);
        if (start > end)
        {
            (start, end) = (end, start);
        }

        var granularity = period switch
        {
            "hafta" => ReportPeriod.Hafta,
            "ay" => ReportPeriod.Ay,
            _ => ReportPeriod.Gun
        };

        return new ReportViewModel
        {
            From = start,
            To = end,
            Period = granularity,
            Report = await reportService.BuildAsync(
                IstanbulTime.StartOfDayUtc(start),
                IstanbulTime.StartOfDayUtc(end.AddDays(1)),
                granularity,
                IstanbulTime.Zone,
                cancellationToken)
        };
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
