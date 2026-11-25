using System.ComponentModel.DataAnnotations;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.Web.Models.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "E-posta gerekli.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta gir.")]
    [StringLength(256)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gerekli.")]
    [StringLength(IdentitySetup.MaxPasswordLength, MinimumLength = IdentitySetup.MinPasswordLength,
        ErrorMessage = "Şifre {2} ile {1} karakter arasında olmalı.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı gerekli.")]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
