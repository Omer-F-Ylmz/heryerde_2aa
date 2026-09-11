using HerYerde.Business.Abstract;
using HerYerde.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AdminPolicy.Name)]
public class AuditController : Controller
{
    private readonly IAdminAuditService _auditService;

    public AuditController(IAdminAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("admin/denetim")]
    public async Task<IActionResult> Index(int sayfa = 1, CancellationToken cancellationToken = default)
    {
        var (_, page) = await _auditService.GetRecentAsync(sayfa, cancellationToken);
        return View(new AuditListViewModel { Page = page.Data! });
    }
}
