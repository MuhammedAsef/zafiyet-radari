using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ZafiyetRadari.Infrastructure.Identity;

/// <summary>
/// Identity tabloları (auth şeması) ve Data Protection anahtar halkası (ops.DataProtectionKeys).
/// Tablolar db/migrations/0008_identity.sql ile oluşur; EF migration kullanılmaz (DB First).
/// </summary>
internal sealed class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("auth");
        builder.Entity<DataProtectionKey>().ToTable("DataProtectionKeys", "ops");
    }
}
