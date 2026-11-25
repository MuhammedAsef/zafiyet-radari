using FluentValidation;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Inventory;

namespace ZafiyetRadari.Application.Inventory;

// Girdi biçimi burada, iş kuralları (son sahip, tekil ad...) Domain'de doğrulanır.
// Uzunluk sınırları Domain sabitlerinden gelir; veritabanı kolon boyutlarıyla aynıdır.

internal sealed class CreateAssetGroupCommandValidator : AbstractValidator<CreateAssetGroupCommand>
{
    public CreateAssetGroupCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Grup adı zorunlu.")
            .MaximumLength(AssetGroup.MaxNameLength).WithMessage($"Grup adı en fazla {AssetGroup.MaxNameLength} karakter olabilir.");
        RuleFor(x => x.Description).MaximumLength(AssetGroup.MaxDescriptionLength)
            .WithMessage($"Açıklama en fazla {AssetGroup.MaxDescriptionLength} karakter olabilir.");
    }
}

internal sealed class UpdateAssetGroupCommandValidator : AbstractValidator<UpdateAssetGroupCommand>
{
    public UpdateAssetGroupCommandValidator()
    {
        RuleFor(x => x.AssetGroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Grup adı zorunlu.")
            .MaximumLength(AssetGroup.MaxNameLength).WithMessage($"Grup adı en fazla {AssetGroup.MaxNameLength} karakter olabilir.");
        RuleFor(x => x.Description).MaximumLength(AssetGroup.MaxDescriptionLength)
            .WithMessage($"Açıklama en fazla {AssetGroup.MaxDescriptionLength} karakter olabilir.");
    }
}

internal sealed class AddGroupMemberCommandValidator : AbstractValidator<AddGroupMemberCommand>
{
    public AddGroupMemberCommandValidator()
    {
        RuleFor(x => x.AssetGroupId).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunlu.")
            .MaximumLength(256).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");
        RuleFor(x => x.Role).IsInEnum().WithMessage("Geçersiz rol.");
    }
}

internal sealed class ChangeGroupMemberRoleCommandValidator : AbstractValidator<ChangeGroupMemberRoleCommand>
{
    public ChangeGroupMemberRoleCommandValidator()
    {
        RuleFor(x => x.AssetGroupId).GreaterThan(0);
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Role).IsInEnum().WithMessage("Geçersiz rol.");
    }
}

internal sealed class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator()
    {
        RuleFor(x => x.AssetGroupId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Varlık adı zorunlu.")
            .MaximumLength(Asset.MaxNameLength).WithMessage($"Varlık adı en fazla {Asset.MaxNameLength} karakter olabilir.");
        RuleFor(x => x.Environment).IsInEnum().WithMessage("Geçersiz ortam.");
        RuleFor(x => x.Criticality).IsInEnum().WithMessage("Geçersiz kritiklik.");
    }
}

internal sealed class UpdateAssetCommandValidator : AbstractValidator<UpdateAssetCommand>
{
    public UpdateAssetCommandValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Varlık adı zorunlu.")
            .MaximumLength(Asset.MaxNameLength).WithMessage($"Varlık adı en fazla {Asset.MaxNameLength} karakter olabilir.");
        RuleFor(x => x.Environment).IsInEnum().WithMessage("Geçersiz ortam.");
        RuleFor(x => x.Criticality).IsInEnum().WithMessage("Geçersiz kritiklik.");
    }
}

internal sealed class AddComponentCommandValidator : AbstractValidator<AddComponentCommand>
{
    public AddComponentCommandValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Ecosystem).Must(e => Ecosystems.All.Contains(e, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Ekosistem npm veya NuGet olmalı.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Paket adı zorunlu.")
            .MaximumLength(PackageCoordinate.MaxNpmNameLength).WithMessage("Paket adı çok uzun.");
        RuleFor(x => x.Version).NotEmpty().WithMessage("Sürüm zorunlu.")
            .MaximumLength(PackageCoordinate.MaxVersionLength).WithMessage("Sürüm çok uzun.");
    }
}

internal sealed class UploadInventoryCommandValidator : AbstractValidator<UploadInventoryCommand>
{
    public UploadInventoryCommandValidator()
    {
        RuleFor(x => x.AssetId).GreaterThan(0);
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Dosya adı boş.")
            .MaximumLength(InventoryUpload.MaxFileNameLength * 4) // yol bilgisiyle gelebilir; sonra kırpılır
            .Must(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Sadece .json uzantılı dosyalar yüklenebilir.");
    }
}
