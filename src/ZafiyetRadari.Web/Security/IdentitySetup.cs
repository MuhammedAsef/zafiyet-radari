using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Infrastructure.Identity;

namespace ZafiyetRadari.Web.Security;

public static class IdentitySetup
{
    public const int MinPasswordLength = PasswordPolicy.MinLength;
    public const int MaxPasswordLength = PasswordPolicy.MaxLength;

    public static IServiceCollection AddWebIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                // Uzunluk burada; "4 gruptan en az 3'ü" ve "e-posta adını içermez" kuralları
                // PasswordCompositionValidator'da (Identity'nin Require* seçenekleri her grubu tek tek zorunlu kılar).
                options.Password.RequiredLength = MinPasswordLength;
                options.Password.RequiredUniqueChars = 4;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                // Brute force: 5 hatalı denemede 15 dakika kilit (IP bazlı rate limiting'e ek olarak).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;

                // Giriş için doğrulama zorunlu değil (mevcut hesaplar kilitlenmesin); doğrulanmamış adrese bildirim gönderilmez.
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddInfrastructureStores()
            .AddPasswordValidator<PasswordCompositionValidator>()
            .AddErrorDescriber<TurkishIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        // E-posta doğrulama linki 24 saat geçerli (varsayılan 1 gün; açıkça belirtildi).
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(24));

        // Rol veya şifre değişince eski oturumlar en geç 5 dakikada düşer.
        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(5));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-zr-auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        var dataProtection = services.AddDataProtection()
            .SetApplicationName("ZafiyetRadari")
            .PersistKeysToDatabase();

        var certificate = DataProtectionCertificate.Load(configuration, environment);
        if (certificate is not null)
        {
            // Unprotect...: Linux'ta sertifika kullanıcı deposunda olmadığı için çözme işlemi de bu örneği kullanmalı.
            dataProtection
                .ProtectKeysWithCertificate(certificate)
                .UnprotectKeysWithAnyCertificate(certificate);
        }

        services.AddAuthorization(options =>
        {
            // Varsayılan kapalı: herkese açık sayfalar [AllowAnonymous] ile tek tek işaretlenir.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(Policies.RequireAdmin, policy => policy.RequireRole(AppRoles.Admin));
        });

        services.AddScoped<IAuthorizationHandler, AssetGroupAccessHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationResultHandler>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}
