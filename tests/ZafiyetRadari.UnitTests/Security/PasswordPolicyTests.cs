using Microsoft.AspNetCore.Identity;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.UnitTests.Security;

/// <summary>Şifre kuralları: "4 gruptan en az 3'ü" ve "e-posta adını içermez". JS tarafı aynı kuralları gösterir.</summary>
public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("abcdefghijkl", 1)]          // sadece küçük
    [InlineData("Abcdefghijkl", 2)]          // büyük + küçük
    [InlineData("Abcdefghijk1", 3)]          // + rakam
    [InlineData("#Maserati2026", 4)]         // + özel
    [InlineData("ÇĞİÖŞÜ çğıöşü", 3)]         // Türkçe harfler + boşluk (özel karakter sayılır)
    [InlineData("1234567890123", 1)]
    public void CountsCategories(string password, int expected) =>
        Assert.Equal(expected, PasswordPolicy.CountCategories(password));

    [Theory]
    [InlineData("Muhammed-2026!", "muhammed@ornek.com", true)]   // büyük/küçük harf fark etmez
    [InlineData("Guclu-Sifre-2026", "muhammed@ornek.com", false)]
    [InlineData("abc-Sifre-2026x", "abc@ornek.com", false)]      // 4 karakterden kısa ad dikkate alınmaz
    [InlineData("Guclu-Sifre-2026", null, false)]
    public void DetectsEmailName(string password, string? email, bool expected) =>
        Assert.Equal(expected, PasswordPolicy.ContainsEmailName(password, email));

    [Theory]
    [InlineData("Abcdefghijk1", true)]
    [InlineData("#Maserati2026", true)]
    [InlineData("abcdefghijkl1", false)]     // 2 grup
    [InlineData("deneme-sifre-uzun", false)] // küçük + özel = 2 grup
    public async Task Validator_RequiresThreeCategories(string password, bool expectedValid)
    {
        var result = await Validate(password, "kullanici@ornek.com");

        Assert.Equal(expectedValid, result.Succeeded);
        if (!expectedValid)
        {
            Assert.Contains(result.Errors, e => e.Code == "PasswordCategories");
        }
    }

    [Fact]
    public async Task Validator_RejectsEmailName()
    {
        var result = await Validate("Muhammed#2026x", "muhammed@ornek.com");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "PasswordContainsEmail");
    }

    [Fact]
    public async Task Validator_RejectsTooLong()
    {
        var result = await Validate("Aa1!" + new string('x', PasswordPolicy.MaxLength), "kullanici@ornek.com");

        Assert.Contains(result.Errors, e => e.Code == "PasswordTooLong");
    }

    private static Task<IdentityResult> Validate(string password, string email) =>
        new PasswordCompositionValidator().ValidateAsync(
            manager: null!,
            new IdentityUser { UserName = email, Email = email },
            password);
}
