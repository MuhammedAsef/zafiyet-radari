using Microsoft.AspNetCore.Identity;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Identity'nin yerleşik doğrulayıcısı "her grup zorunlu" mantığıyla çalışır; burada istenen
/// "dört gruptan en az üçü" kuralı ve e-posta adını içermeme kuralı ayrıca uygulanır.
/// Uzunluk kontrolü yerleşik doğrulayıcıda kalır (RequiredLength).
/// </summary>
public sealed class PasswordCompositionValidator : IPasswordValidator<IdentityUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            // Boş şifreyi yerleşik doğrulayıcı raporlar; aynı hatayı iki kez göstermeyelim.
            return Task.FromResult(IdentityResult.Success);
        }

        var errors = new List<IdentityError>();

        if (password.Length > PasswordPolicy.MaxLength)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = $"Şifre en fazla {PasswordPolicy.MaxLength} karakter olabilir.",
            });
        }

        if (PasswordPolicy.CountCategories(password) < PasswordPolicy.RequiredCategories)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordCategories",
                Description = $"Şifre büyük harf, küçük harf, rakam ve özel karakter gruplarından en az {PasswordPolicy.RequiredCategories} tanesini içermeli.",
            });
        }

        if (PasswordPolicy.ContainsEmailName(password, user.Email ?? user.UserName))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordContainsEmail",
                Description = "Şifre e-posta adresinin @ öncesindeki kısmını içermemeli.",
            });
        }

        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]));
    }
}
