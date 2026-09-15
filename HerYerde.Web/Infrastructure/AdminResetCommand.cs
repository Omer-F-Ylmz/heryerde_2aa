using System.Net;
using HerYerde.Business.Abstract;

namespace HerYerde.Web.Infrastructure;

/// <summary>"--admin-sifirla e-posta": parolasını ve iki adımlı cihazını kaybeden yönetici için sunucudan kurtarma.
/// Geçici parola konsola yazılır (postaya değil), ilk girişte değiştirilmesi zorunludur; açık oturumlar düşer.</summary>
public static class AdminResetCommand
{
    public const string Argument = "--admin-sifirla";

    public static string? EmailFrom(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    public static async Task<string> RunAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var (status, result) = await scope.ServiceProvider.GetRequiredService<IAdminAuthService>()
            .ResetWithTemporaryPasswordAsync(email.Trim());
        return status == HttpStatusCode.OK
            ? result.Data!
            : throw new InvalidOperationException(result.Message);
    }
}
