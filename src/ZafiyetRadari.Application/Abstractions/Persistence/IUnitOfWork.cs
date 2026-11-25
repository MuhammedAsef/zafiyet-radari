namespace ZafiyetRadari.Application.Abstractions.Persistence;

/// <summary>
/// Bir iş adımındaki tüm değişiklikleri tek transaction'da kaydeder.
/// Repository'ler değişiklikleri hazırlar, kaydetme kararı Application servisindedir.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
