using Microsoft.AspNetCore.Mvc;
using ZafiyetRadari.Domain.Common;

namespace ZafiyetRadari.Web.Controllers;

/// <summary>
/// Application servislerinin Result'larını HTTP'ye çevirir. Yetki kontrolü servislerdedir (üyelik + rol, audit dahil);
/// controller sadece sonucu yorumlar. Tüm alanlar FallbackPolicy ile oturum ister.
/// </summary>
public abstract class AppControllerBase : Controller
{
    private const string ValidationPrefix = "Validation.";

    /// <summary>NotFound/Forbidden için hazır yanıt; diğer hata türlerinde null (çağıran formu tekrar gösterir).</summary>
    protected IActionResult? AccessFailure(Error error) => error.Type switch
    {
        ErrorType.NotFound => (IActionResult)NotFound(),
        ErrorType.Forbidden => Forbid(),
        _ => null
    };

    /// <summary>"Validation.Name" kodlu hata Name alanının altına, diğerleri formun üstüne yazılır.</summary>
    protected void AddError(Error error)
    {
        var key = error.Code.StartsWith(ValidationPrefix, StringComparison.Ordinal) ? error.Code[ValidationPrefix.Length..] : string.Empty;
        ModelState.AddModelError(key, error.Message);
    }

    protected void FlashSuccess(string message) => TempData["Success"] = message;

    protected void FlashError(string message) => TempData["Error"] = message;

    /// <summary>Formsuz POST işlemleri (sil, rol değiştir) için: erişim hatası değilse mesajla geri dön.</summary>
    protected IActionResult RedirectWithResult(Result result, string successMessage, string action, object routeValues)
    {
        if (result.IsFailure)
        {
            var failure = AccessFailure(result.Error);
            if (failure is not null)
            {
                return failure;
            }

            FlashError(result.Error.Message);
        }
        else
        {
            FlashSuccess(successMessage);
        }

        return RedirectToAction(action, routeValues);
    }
}
