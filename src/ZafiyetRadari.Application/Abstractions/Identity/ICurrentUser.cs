namespace ZafiyetRadari.Application.Abstractions.Identity;

/// <summary>İsteği yapan kullanıcı. Application katmanı HttpContext'i bilmez, bu arayüzü kullanır.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? UserName { get; }

    string? IpAddress { get; }
}
