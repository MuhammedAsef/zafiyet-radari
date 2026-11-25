namespace ZafiyetRadari.Application.Abstractions.Identity;

/// <summary>
/// Sistem rolleri (db/migrations/0008_identity.sql ile oluşturulur).
/// Varlık grubu verisine erişim bu rollerle değil, grup üyeliğiyle belirlenir: Admin bile üye olmadığı grubu göremez.
/// </summary>
public static class AppRoles
{
    /// <summary>Kullanıcı ve sistem yönetimi (iş paneli, audit doğrulama). Sadece Migrator CLI ile verilir.</summary>
    public const string Admin = "Admin";

    /// <summary>Varsayılan rol: varlık grubu oluşturur, üye olduğu grupları yönetir.</summary>
    public const string Analyst = "Analyst";

    /// <summary>Sadece okuma.</summary>
    public const string Viewer = "Viewer";
}
