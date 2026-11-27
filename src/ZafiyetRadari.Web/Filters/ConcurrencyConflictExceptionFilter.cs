using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Web.Logging;

namespace ZafiyetRadari.Web.Filters;

/// <summary>
/// Eş zamanlı değişiklik (rowversion / tekil indeks yarışı) beklenen bir durumdur, sunucu hatası değildir:
/// 500 yerine 409 sayfası gösterilir ve uyarı loglanır.
/// </summary>
public sealed class ConcurrencyConflictExceptionFilter(ILogger<ConcurrencyConflictExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ConcurrencyConflictException exception)
        {
            return;
        }

        logger.LogWarning(exception, "Eş zamanlı değişiklik çakışması: {Path}", LogSanitizer.Path(context.HttpContext));
        context.Result = new StatusCodeResult(StatusCodes.Status409Conflict);
        context.ExceptionHandled = true;
    }
}
