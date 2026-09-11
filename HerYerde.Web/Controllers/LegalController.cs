using HerYerde.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

/// <summary>Yasal metinler: statik Razor görünümleri; sürüm ve güncelleme tarihi LegalDocs'tan.</summary>
public class LegalController : Controller
{
    [HttpGet("yasal/{slug}")]
    public IActionResult Page(string slug)
        => LegalPages.Find(slug) is { } page ? View(page.Slug, page) : NotFound();
}
