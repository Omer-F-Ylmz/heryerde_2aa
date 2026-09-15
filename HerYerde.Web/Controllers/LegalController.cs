using HerYerde.Web.Infrastructure;
using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Yasal metinler: statik Razor görünümleri; sürüm ve güncelleme tarihi LegalDocs'tan.</summary>
public class LegalController : Controller
{
    [HttpGet("yasal/{slug}")]
    public IActionResult Page(string slug)
        => LegalPages.Find(slug) is { } page ? View(page.Slug, page) : NotFound();

    /// <summary>Elle doldurulacak boş cayma formu.</summary>
    [HttpGet("yasal/cayma-formu/pdf")]
    public IActionResult WithdrawalForm() => File(LegalPdf.WithdrawalForm(null), "application/pdf", "cayma-formu.pdf");

    /// <summary>Sitede doldurulan cayma formu; alanlar kaydedilmez, yalnız indirilen PDF'e yazılır.</summary>
    [HttpPost("yasal/cayma-formu/pdf")]
    [ValidateAntiForgeryToken]
    public IActionResult WithdrawalForm(WithdrawalFormModel form)
        => ModelState.IsValid
            ? File(LegalPdf.WithdrawalForm(form), "application/pdf", "cayma-formu.pdf")
            : BadRequest();
}
