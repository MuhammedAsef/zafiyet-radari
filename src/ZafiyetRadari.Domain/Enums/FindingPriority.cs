namespace ZafiyetRadari.Domain.Enums;

/// <summary>Kullanıcıya gösterilen aksiyon kovası. Değerler veritabanındaki CHECK constraint'leriyle birebir aynıdır.</summary>
public enum FindingPriority : byte
{
    /// <summary>Hemen ilgilen.</summary>
    Now = 1,

    ThisWeek = 2,

    Plan = 3,

    /// <summary>İzle.</summary>
    Watch = 4
}
