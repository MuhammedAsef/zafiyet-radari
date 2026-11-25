namespace ZafiyetRadari.Application.Abstractions.Identity;

/// <summary>Kullanıcı arama. Identity tablolarına erişim Infrastructure'dadır.</summary>
public interface IUserDirectory
{
    /// <summary>E-posta ile kullanıcı kimliği; yoksa null. Karşılaştırma normalize edilmiş e-posta ile yapılır.</summary>
    Task<string?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Kimlik → e-posta, sadece e-postası doğrulanmış kullanıcılar için.</summary>
    Task<IReadOnlyDictionary<string, string>> GetConfirmedEmailsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);

    /// <summary>Kimlik → görünen ad (e-posta). Bulunamayanlar sözlükte yer almaz.</summary>
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}
