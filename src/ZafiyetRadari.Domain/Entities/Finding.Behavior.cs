using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;

namespace ZafiyetRadari.Domain.Entities;

/// <summary>
/// Bulgunun iş kuralları. Scaffold edilen Finding.cs'e dokunulmaz; durum değişiklikleri sadece bu metotlarla yapılır.
/// Her geçiş FindingHistory'ye yazılır.
/// </summary>
public partial class Finding
{
    public const int MaxCommentLength = 1000;
    public const int MaxRiskAcceptanceDays = 180;

    public FindingStatus CurrentStatus => (FindingStatus)Status;

    public FindingPriority CurrentPriority => (FindingPriority)Priority;

    public MatchConfidenceLevel Confidence => (MatchConfidenceLevel)MatchConfidence;

    /// <summary>Kullanıcının aksiyon alması beklenen durumlar.</summary>
    public bool IsActionable => CurrentStatus is FindingStatus.Open or FindingStatus.InReview;

    public static Result<Finding> Create(
        long componentId,
        int vulnerabilityId,
        MatchConfidenceLevel confidence,
        decimal riskScore,
        FindingPriority priority,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(confidence) || !Enum.IsDefined(priority))
        {
            return FindingErrors.InvalidClassification;
        }

        if (riskScore is < 0 or > 100)
        {
            return FindingErrors.InvalidRiskScore;
        }

        var finding = new Finding
        {
            ComponentId = componentId,
            VulnerabilityId = vulnerabilityId,
            Status = (byte)FindingStatus.Open,
            MatchConfidence = (byte)confidence,
            RiskScore = decimal.Round(riskScore, 2),
            Priority = (byte)priority,
            FirstSeenAt = now,
            LastSeenAt = now
        };

        finding.FindingHistories.Add(new FindingHistory
        {
            FromStatus = null,
            ToStatus = (byte)FindingStatus.Open,
            Comment = "Eşleştirme motoru tarafından oluşturuldu.",
            ChangedAt = now
        });

        return finding;
    }

    /// <summary>Kullanıcının yaptığı durum değişikliği.</summary>
    public Result ChangeStatus(
        FindingStatus newStatus,
        string? userId,
        string? comment,
        DateOnly? riskAcceptedUntil,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(newStatus))
        {
            return Result.Failure(FindingErrors.InvalidStatus);
        }

        if (newStatus == CurrentStatus)
        {
            return Result.Failure(FindingErrors.StatusUnchanged);
        }

        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment is { Length: > MaxCommentLength })
        {
            return Result.Failure(FindingErrors.CommentTooLong);
        }

        // Riski kabul etmek ve "yanlış alarm" demek iz bırakmalı: gerekçesiz yapılamaz.
        if ((newStatus is FindingStatus.RiskAccepted or FindingStatus.FalsePositive) && comment is null)
        {
            return Result.Failure(FindingErrors.JustificationRequired);
        }

        if (newStatus == FindingStatus.RiskAccepted)
        {
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (riskAcceptedUntil is null || riskAcceptedUntil <= today)
            {
                return Result.Failure(FindingErrors.RiskAcceptanceDateRequired);
            }

            if (riskAcceptedUntil > today.AddDays(MaxRiskAcceptanceDays))
            {
                return Result.Failure(FindingErrors.RiskAcceptanceTooLong);
            }
        }

        Transition(newStatus, userId, comment, now);
        RiskAcceptedUntil = newStatus == FindingStatus.RiskAccepted ? riskAcceptedUntil : null;
        ResolvedAt = newStatus is FindingStatus.Fixed or FindingStatus.FalsePositive ? now : null;

        return Result.Success();
    }

    /// <summary>Eşleştirme motoru bileşeni hâlâ etkilenen sürümde gördü.</summary>
    public void MarkSeen(DateTimeOffset now)
    {
        LastSeenAt = now;

        if (CurrentStatus == FindingStatus.Fixed)
        {
            Reopen("Düzeltildi olarak işaretlenmişti ancak bileşen hâlâ etkilenen sürümde (regresyon).", now);
        }
    }

    /// <summary>Eşleştirme motoru bileşeni artık etkilenen sürümde görmüyor.</summary>
    public void ResolveBySystem(DateTimeOffset now)
    {
        if (!IsActionable)
        {
            return;
        }

        Transition(FindingStatus.Fixed, userId: null, "Eşleşme artık yok: bileşen güncellendi/kaldırıldı veya zafiyet kaydı değişti.", now);
        ResolvedAt = now;
    }

    /// <summary>Süresi dolan risk kabulünü yeniden açar. Açıldıysa true döner.</summary>
    public bool ExpireRiskAcceptance(DateTimeOffset now)
    {
        if (CurrentStatus != FindingStatus.RiskAccepted
            || RiskAcceptedUntil is null
            || RiskAcceptedUntil >= DateOnly.FromDateTime(now.UtcDateTime))
        {
            return false;
        }

        Reopen($"Risk kabulünün süresi doldu ({RiskAcceptedUntil:yyyy-MM-dd}).", now);
        return true;
    }

    public Result UpdateRisk(decimal riskScore, FindingPriority priority)
    {
        if (riskScore is < 0 or > 100)
        {
            return Result.Failure(FindingErrors.InvalidRiskScore);
        }

        if (!Enum.IsDefined(priority))
        {
            return Result.Failure(FindingErrors.InvalidClassification);
        }

        RiskScore = decimal.Round(riskScore, 2);
        Priority = (byte)priority;
        return Result.Success();
    }

    /// <summary>Zafiyet kaydındaki aralıklar değişince eşleşme güveni değişebilir (ör. okunamayan aralık düzeltildi).</summary>
    public void UpdateConfidence(MatchConfidenceLevel confidence)
    {
        if (Enum.IsDefined(confidence))
        {
            MatchConfidence = (byte)confidence;
        }
    }

    private void Reopen(string reason, DateTimeOffset now)
    {
        Transition(FindingStatus.Open, userId: null, reason, now);
        ResolvedAt = null;
        RiskAcceptedUntil = null;
    }

    private void Transition(FindingStatus to, string? userId, string? comment, DateTimeOffset now)
    {
        FindingHistories.Add(new FindingHistory
        {
            FromStatus = Status,
            ToStatus = (byte)to,
            Comment = comment,
            ChangedByUserId = userId,
            ChangedAt = now
        });

        Status = (byte)to;
    }
}
