using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ZafiyetRadari.Infrastructure.Identity;

/// <summary>Web'in Identity ve Data Protection kurulumunu, AuthDbContext'i dışarı açmadan bağlar.</summary>
public static class IdentityStoreExtensions
{
    public static IdentityBuilder AddInfrastructureStores(this IdentityBuilder builder) =>
        builder.AddEntityFrameworkStores<AuthDbContext>();

    /// <summary>
    /// Anahtarlar veritabanında tutulur: uygulama yeniden başlasa veya birden fazla kopya çalışsa da
    /// oturum ve antiforgery cookie'leri geçerli kalır. Production'da anahtarlar ayrıca sertifikayla şifrelenmeli.
    /// </summary>
    public static IDataProtectionBuilder PersistKeysToDatabase(this IDataProtectionBuilder builder) =>
        builder.PersistKeysToDbContext<AuthDbContext>();
}
