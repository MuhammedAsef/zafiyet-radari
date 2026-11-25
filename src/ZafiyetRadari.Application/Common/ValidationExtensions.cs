using FluentValidation;
using ZafiyetRadari.Domain.Common;

namespace ZafiyetRadari.Application.Common;

internal static class ValidationExtensions
{
    /// <summary>FluentValidation sonucunu Result'a çevirir. İlk hata döner; alan adı hata koduna eklenir ki UI doğru alana yazabilsin.</summary>
    public static async Task<Result> ValidateToResultAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(instance, cancellationToken);
        if (validation.IsValid)
        {
            return Result.Success();
        }

        var failure = validation.Errors[0];
        return Result.Failure(Error.Validation($"Validation.{failure.PropertyName}", failure.ErrorMessage));
    }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
