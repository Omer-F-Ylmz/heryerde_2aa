using Microsoft.AspNetCore.Mvc;

namespace HerYerde.Web.Controllers;

public class HomeController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => Problem(statusCode: 500, title: "Beklenmeyen bir hata oluştu.");

    /// <summary>UseStatusCodePagesWithReExecute hedefi: 404 ve 429 markalı sayfa, diğerleri Problem.</summary>
    [Route("hata/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Hata(int code)
    {
        var branded = code switch
        {
            StatusCodes.Status404NotFound => "NotFound",
            StatusCodes.Status429TooManyRequests => "TooManyRequests",
            _ => null
        };

        if (branded is not null)
        {
            Response.StatusCode = code;
            return View(branded);
        }

        return Problem(statusCode: code);
    }
}
