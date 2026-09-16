using HerYerde.Business.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

/// <summary>Arama raporu (D15): son 30 günde sonuç bulunamayan ve en çok aranan terimler.</summary>
[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class SearchReportController(ISearchLogService searchLogs) : Controller
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    [HttpGet("admin/arama")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(await searchLogs.GetReportAsync(Window, cancellationToken));
}
