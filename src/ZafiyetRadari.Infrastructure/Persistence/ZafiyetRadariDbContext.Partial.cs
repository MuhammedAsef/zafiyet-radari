using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Domain.Entities;

namespace ZafiyetRadari.Infrastructure.Persistence;

/// <summary>
/// Scaffold edilen DbContext'in elle yazılan kısmı. ZafiyetRadariDbContext.cs her scaffold'da ezilir, bu dosya ezilmez.
/// Ek fluent konfigürasyon gerekirse OnModelCreatingPartial burada implemente edilir.
/// </summary>
public partial class ZafiyetRadariDbContext : IUnitOfWork
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditLogIsNotModified();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureAuditLogIsNotModified();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("Kayıt okunduktan sonra değiştirildi (rowversion).", exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConcurrencyConflictException("Aynı tekil kayıt eş zamanlı eklendi.", exception);
        }
    }

    // 2601: tekil indeks ihlali, 2627: tekil/PK kısıtı ihlali.
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    // Derinlemesine savunma: veritabanı zaten DENY ile engelliyor; bu kontrol hatayı kod tarafında, erken ve açık mesajla yakalar.
    private void EnsureAuditLogIsNotModified()
    {
        var touched = ChangeTracker.Entries<AuditLog>()
            .Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

        if (touched)
        {
            throw new InvalidOperationException(
                "Audit kayıtları EF üzerinden yazılamaz. Ekleme için IAuditTrail kullanılır; güncelleme ve silme yasaktır.");
        }
    }
}
