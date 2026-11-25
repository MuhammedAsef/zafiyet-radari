using ZafiyetRadari.Domain.Common;

namespace ZafiyetRadari.Domain.Errors;

public static class FindingErrors
{
    /// <summary>Bulgu yok veya kullanıcının grubunda değil: ikisi aynı yanıtı alır.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Finding.NotFound", "Bulgu bulunamadı.");

    public static readonly Error InvalidStatus =
        Error.Validation("Finding.InvalidStatus", "Geçersiz bulgu durumu.");

    public static readonly Error StatusUnchanged =
        Error.Conflict("Finding.StatusUnchanged", "Bulgu zaten bu durumda.");

    public static readonly Error JustificationRequired =
        Error.Validation("Finding.JustificationRequired", "Bu durum değişikliği için gerekçe yazılmalı.");

    public static readonly Error CommentTooLong =
        Error.Validation("Finding.CommentTooLong", $"Açıklama en fazla {Entities.Finding.MaxCommentLength} karakter olabilir.");

    public static readonly Error RiskAcceptanceDateRequired =
        Error.Validation("Finding.RiskAcceptanceDateRequired", "Risk kabulü için ileri tarihli bir bitiş tarihi gerekli.");

    public static readonly Error RiskAcceptanceTooLong =
        Error.Validation("Finding.RiskAcceptanceTooLong", $"Risk kabulü en fazla {Entities.Finding.MaxRiskAcceptanceDays} gün olabilir.");

    public static readonly Error InvalidRiskScore =
        Error.Validation("Finding.InvalidRiskScore", "Risk skoru 0 ile 100 arasında olmalı.");

    public static readonly Error InvalidClassification =
        Error.Validation("Finding.InvalidClassification", "Geçersiz öncelik veya eşleşme güveni.");
}
