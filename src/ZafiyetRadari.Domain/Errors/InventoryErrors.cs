using ZafiyetRadari.Domain.Common;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Domain.Errors;

public static class AssetGroupErrors
{
    public static readonly Error NameRequired =
        Error.Validation("AssetGroup.NameRequired", "Grup adı boş olamaz.");

    public static readonly Error NameTooLong =
        Error.Validation("AssetGroup.NameTooLong", $"Grup adı en fazla {AssetGroup.MaxNameLength} karakter olabilir.");

    public static readonly Error DescriptionTooLong =
        Error.Validation("AssetGroup.DescriptionTooLong", $"Açıklama en fazla {AssetGroup.MaxDescriptionLength} karakter olabilir.");

    public static readonly Error OwnerRequired =
        Error.Validation("AssetGroup.OwnerRequired", "Grubun bir sahibi olmalı.");

    /// <summary>Üye olmayan kullanıcıya da bu döner: grubun var olup olmadığı sızdırılmaz.</summary>
    public static readonly Error NotFound =
        Error.NotFound("AssetGroup.NotFound", "Varlık grubu bulunamadı.");

    public static readonly Error Forbidden =
        Error.Forbidden("AssetGroup.Forbidden", "Bu işlem için grupta yeterli yetkiniz yok.");

    public static readonly Error InvalidRole =
        Error.Validation("AssetGroup.InvalidRole", "Geçersiz grup rolü.");

    public static readonly Error MemberAlreadyExists =
        Error.Conflict("AssetGroup.MemberAlreadyExists", "Bu kullanıcı zaten grubun üyesi.");

    // Form hataları: sayfa bulunamadı (404) değil, kullanıcıya mesaj olarak gösterilir.
    public static readonly Error MemberNotFound =
        Error.Validation("AssetGroup.MemberNotFound", "Bu kullanıcı grubun üyesi değil.");

    public static readonly Error LastOwner =
        Error.Conflict("AssetGroup.LastOwner", "Grubun son sahibi çıkarılamaz veya rolü düşürülemez.");

    public static readonly Error UserNotFound =
        Error.Validation("AssetGroup.UserNotFound", "Bu e-posta adresiyle kayıtlı kullanıcı bulunamadı.");
}

public static class AssetErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Asset.NameRequired", "Varlık adı boş olamaz.");

    public static readonly Error NameTooLong =
        Error.Validation("Asset.NameTooLong", $"Varlık adı en fazla {Asset.MaxNameLength} karakter olabilir.");

    public static readonly Error InvalidClassification =
        Error.Validation("Asset.InvalidClassification", "Geçersiz ortam veya kritiklik değeri.");

    public static readonly Error NotFound =
        Error.NotFound("Asset.NotFound", "Varlık bulunamadı.");

    public static readonly Error NameTaken =
        Error.Conflict("Asset.NameTaken", "Bu grupta aynı adda bir varlık zaten var.");
}

public static class ComponentErrors
{
    public static readonly Error UnsupportedEcosystem =
        Error.Validation("Component.UnsupportedEcosystem", "Desteklenmeyen ekosistem. Şimdilik sadece npm ve NuGet destekleniyor.");

    public static readonly Error InvalidName =
        Error.Validation("Component.InvalidName", "Geçersiz paket adı.");

    public static readonly Error InvalidVersion =
        Error.Validation("Component.InvalidVersion",
            $"Geçersiz sürüm. Tam bir sürüm girilmeli (örn. 4.17.21); aralıklar (^1.2, ~2.0, latest) kabul edilmez. En fazla {PackageCoordinate.MaxVersionLength} karakter.");

    public static readonly Error AlreadyExists =
        Error.Conflict("Component.AlreadyExists", "Bu paket sürümü varlıkta zaten kayıtlı.");

    public static readonly Error NotFound =
        Error.NotFound("Component.NotFound", "Bileşen bulunamadı.");
}

public static class UploadErrors
{
    public static readonly Error Empty =
        Error.Validation("Upload.Empty", "Dosya boş.");

    public static readonly Error InvalidFileName =
        Error.Validation("Upload.InvalidFileName", "Sadece .json uzantılı dosyalar yüklenebilir.");

    public static readonly Error InvalidJson =
        Error.Validation("Upload.InvalidJson", "Dosya geçerli bir JSON değil veya izin verilen iç içe derinliği aşıyor.");

    public static readonly Error UnsupportedFormat =
        Error.Validation("Upload.UnsupportedFormat",
            "Dosya biçimi tanınmadı. Desteklenenler: CycloneDX JSON SBOM, package-lock.json (v1-v3), packages.lock.json.");

    public static readonly Error NoComponents =
        Error.Validation("Upload.NoComponents", "Dosyada npm veya NuGet paketi bulunamadı.");

    public static readonly Error AlreadyUploaded =
        Error.Conflict("Upload.AlreadyUploaded", "Bu dosyanın aynısı bu varlığa daha önce yüklenmiş.");

    public static Error TooLarge(int maxMegabytes) =>
        Error.Validation("Upload.TooLarge", $"Dosya en fazla {maxMegabytes} MB olabilir.");

    public static Error TooManyComponents(int max) =>
        Error.Validation("Upload.TooManyComponents", $"Dosyada en fazla {max:N0} paket olabilir.");
}

public static class ConcurrencyErrors
{
    public static readonly Error Conflict =
        Error.Conflict("Concurrency.Conflict", "Kayıt siz işlem yaparken başka biri tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin.");
}
