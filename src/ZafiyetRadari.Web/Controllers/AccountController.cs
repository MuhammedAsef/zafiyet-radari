using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Abstractions.Notifications;
using ZafiyetRadari.Application.Notifications;
using ZafiyetRadari.Web.Models.Account;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.Web.Controllers;

public sealed class AccountController(
    SignInManager<IdentityUser> signInManager,
    UserManager<IdentityUser> userManager,
    IAuditTrail auditTrail,
    ICurrentUser currentUser,
    IEmailSender emailSender,
    IAppLinks appLinks,
    ILogger<AccountController> logger) : Controller
{
    // Kullanıcı var mı yok mu belli etmemek için tek ve aynı mesaj.
    private const string InvalidCredentialsMessage = "E-posta veya şifre hatalı.";

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) =>
        IsSignedIn
            ? RedirectToLocal(returnUrl)
            : View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            model.Email, model.Password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await userManager.FindByNameAsync(model.Email);
            await AuditAsync(AuditEventTypes.LoginSucceeded, user?.Id, model.Email, cancellationToken);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await AuditAsync(AuditEventTypes.LockedOut, userId: null, model.Email, cancellationToken);
            ModelState.AddModelError(string.Empty, "Çok fazla başarısız deneme. Hesap geçici olarak kilitlendi, daha sonra tekrar dene.");
            return View(model);
        }

        await AuditAsync(AuditEventTypes.LoginFailed, userId: null, model.Email, cancellationToken);
        ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register() =>
        IsSignedIn ? RedirectToHome() : View(new RegisterViewModel());

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        // Oturum açıkken (ör. mobilde sayfa yenilenip form tekrar gönderilirse) ikinci hesap açılmaz.
        if (IsSignedIn)
        {
            return RedirectToHome();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var created = await userManager.CreateAsync(user, model.Password);

        if (!created.Succeeded)
        {
            // DuplicateUserName ve DuplicateEmail aynı mesajı üretir (kullanıcı adı = e-posta); her mesaj bir kez gösterilir.
            foreach (var description in created.Errors.Select(e => e.Description).Distinct(StringComparer.Ordinal))
            {
                ModelState.AddModelError(string.Empty, description);
            }

            return View(model);
        }

        // Kayıt olan herkes Analyst. Admin yetkisi uygulamadan verilemez (Migrator CLI: --grant-admin).
        await userManager.AddToRoleAsync(user, AppRoles.Analyst);
        await AuditAsync(AuditEventTypes.UserRegistered, user.Id, user.Email, cancellationToken);

        await signInManager.SignInAsync(user, isPersistent: false);

        // Mail gönderilemese de kayıt geçerli; doğrulama profil sayfasından tekrar istenebilir.
        var sent = await TrySendConfirmationAsync(user, cancellationToken);
        TempData[sent ? "Success" : "Error"] = LastConfirmationMessage;

        return RedirectToHome();
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        ViewData["Email"] = user.Email;
        ViewData["EmailConfirmed"] = user.EmailConfirmed;
        ViewData["Roles"] = string.Join(", ", await userManager.GetRolesAsync(user));
        return View();
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> ResendConfirmation(CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        if (user.EmailConfirmed)
        {
            TempData["Success"] = "E-posta adresin zaten doğrulanmış.";
        }
        else
        {
            var sent = await TrySendConfirmationAsync(user, cancellationToken);
            TempData[sent ? "Success" : "Error"] = LastConfirmationMessage;
        }

        return RedirectToAction(nameof(Profile));
    }

    /// <summary>
    /// Maildeki link. Oturum gerektirmez (başka cihazdan açılabilir); güvenliği 24 saatlik, kullanıcıya bağlı, imzalı token sağlar.
    /// Geçersiz/eksik parametrelerde ayrıntı verilmez.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code, CancellationToken cancellationToken)
    {
        ViewData["Confirmed"] = false;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code) || userId.Length > 450 || code.Length > 2000)
        {
            return View();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return View();
        }

        // Identity'nin doğrulama token'ı süresi dolana kadar geçerlidir (tek kullanımlık değil).
        // Zaten doğrulanmış hesapta tekrar işlem yapılmaz, audit log'a tekrar yazılmaz.
        if (user.EmailConfirmed)
        {
            ViewData["AlreadyConfirmed"] = true;
            return View();
        }

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return View();
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            ViewData["Confirmed"] = true;
            await AuditAsync(AuditEventTypes.EmailConfirmed, user.Id, user.Email, cancellationToken);
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await AuditAsync(AuditEventTypes.Logout, currentUser.UserId, currentUser.UserName, cancellationToken);
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private string LastConfirmationMessage { get; set; } = string.Empty;

    private async Task<bool> TrySendConfirmationAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            await emailSender.SendAsync(AccountEmails.Confirmation(user.Email!, appLinks.ConfirmEmail(user.Id, code)), cancellationToken);

            LastConfirmationMessage = $"Doğrulama maili {user.Email} adresine gönderildi. Link 24 saat geçerli.";
            return true;
        }
        catch (Exception ex) when (ex is System.Net.Mail.SmtpException or InvalidOperationException or IOException or FormatException)
        {
            logger.LogError(ex, "Doğrulama maili gönderilemedi: {UserId}", user.Id);
            LastConfirmationMessage = "Doğrulama maili şu an gönderilemedi. Profil sayfasından tekrar deneyebilirsin.";
            return false;
        }
    }

    private bool IsSignedIn => User.Identity?.IsAuthenticated == true;

    private RedirectToActionResult RedirectToHome() =>
        RedirectToAction(nameof(HomeController.Index), "Home");

    // Open redirect önlemi: sadece uygulama içi adreslere dönülür.
    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToHome();

    private Task AuditAsync(string eventType, string? userId, string? userName, CancellationToken cancellationToken) =>
        auditTrail.AppendAsync(
            new AuditEvent(eventType, userId, userName, currentUser.IpAddress, TargetType: "Account"),
            cancellationToken);
}
