using System.Text.RegularExpressions;
using Serilog.Context;

namespace HerYerde.Web.Infrastructure;

/// <summary>Her isteğe izleme kimliği: istemcinin X-Correlation-ID'si güvenli karakterlerdense o, değilse
/// TraceIdentifier. Yanıta yazılır (hata sayfası başlıkları temizlese de) ve istek boyunca her log olayına
/// CorrelationId olarak eklenir.</summary>
public sealed partial class CorrelationIdMiddleware
{
    public const string Header = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex Safe();

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[Header].ToString();
        var id = Safe().IsMatch(incoming) ? incoming : context.TraceIdentifier;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
        {
            await _next(context);
        }
    }
}
