using ZafiyetRadari.Application.Abstractions.Identity;

namespace ZafiyetRadari.Worker.Security;

/// <summary>
/// Arka plan işlerinin "kullanıcısı". Oturum açmış sayılmaz: kullanıcı yetkisi isteyen Application servisleri
/// (envanter vb.) Worker'dan çağrılırsa erişim reddedilir. Audit kayıtlarında "system" olarak görünür.
/// </summary>
internal sealed class SystemCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public string? UserId => null;

    public string? UserName => "system";

    public string? IpAddress => null;
}
