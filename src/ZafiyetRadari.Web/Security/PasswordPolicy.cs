using System.Numerics;

namespace ZafiyetRadari.Web.Security;

/// <summary>
/// Şifre kurallarının tek kaynağı. Sunucu tarafında <see cref="PasswordCompositionValidator"/>,
/// kayıt formunda wwwroot/js/password-rules.js aynı kuralları uygular (JS sadece anlık geri bildirim içindir;
/// asıl karar her zaman sunucuda verilir).
///
/// Kurallar:
///   1. En az <see cref="MinLength"/>, en fazla <see cref="MaxLength"/> karakter.
///   2. Büyük harf, küçük harf, rakam, özel karakter gruplarından en az <see cref="RequiredCategories"/> tanesi.
///   3. E-posta adresinin kullanıcı adı kısmını (en az <see cref="MinEmailFragmentLength"/> karakterse) içermez.
/// Harf kontrolleri Unicode kategorileriyle yapılır: Ç, Ğ, İ, Ö, Ş, Ü de büyük harf sayılır.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>Çok uzun şifreyle hash hesaplatıp sunucuyu yorma (DoS) önlemi.</summary>
    public const int MaxLength = 128;

    public const int RequiredCategories = 3;
    public const int TotalCategories = 4;
    public const int MinEmailFragmentLength = 4;

    [Flags]
    public enum Categories
    {
        None = 0,
        Upper = 1,
        Lower = 2,
        Digit = 4,
        Symbol = 8,
    }

    public static Categories CategoriesOf(string password)
    {
        var found = Categories.None;

        foreach (var c in password)
        {
            if (char.IsUpper(c))
            {
                found |= Categories.Upper;
            }
            else if (char.IsLower(c))
            {
                found |= Categories.Lower;
            }
            else if (char.IsDigit(c))
            {
                found |= Categories.Digit;
            }
            else if (!char.IsLetter(c))
            {
                // Boşluk da özel karakter sayılır: uzun parola cümleleri bu grubu doğal olarak karşılar.
                found |= Categories.Symbol;
            }
        }

        return found;
    }

    public static int CountCategories(string password) =>
        BitOperations.PopCount((uint)CategoriesOf(password));

    public static bool ContainsEmailName(string password, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.IndexOf('@');
        var localPart = (at >= 0 ? email[..at] : email).Trim();

        return localPart.Length >= MinEmailFragmentLength
            && password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
    }
}
