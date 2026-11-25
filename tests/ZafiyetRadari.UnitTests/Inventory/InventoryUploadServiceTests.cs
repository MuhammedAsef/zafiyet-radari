using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ZafiyetRadari.Application.Abstractions.Auditing;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Application.Abstractions.Identity;
using ZafiyetRadari.Application.Abstractions.Jobs;
using ZafiyetRadari.Application.Abstractions.Persistence;
using ZafiyetRadari.Application.Inventory;
using ZafiyetRadari.Domain.Entities;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Domain.Errors;
using ZafiyetRadari.Infrastructure.Inventory.Parsing;

namespace ZafiyetRadari.UnitTests.Inventory;

public sealed class InventoryUploadServiceTests
{
    private const int GroupId = 7;
    private const int AssetId = 70;

    private const string NpmLock = """
        { "lockfileVersion": 3, "packages": {
            "": { "name": "app" },
            "node_modules/lodash": { "version": "4.17.21" },
            "node_modules/express": { "version": "4.18.2" },
            "node_modules/bad name": { "version": "1.0.0" } } }
        """;

    private readonly FakeAuditTrail _audit = new();
    private readonly FakeRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeScheduler _scheduler = new();

    [Fact]
    public async Task Editor_CanUpload_AndChangesAreSavedAndAudited()
    {
        var service = CreateService("editor", AssetGroupRole.Editor);

        var result = await service.UploadAsync(Command(NpmLock));

        Assert.True(result.IsSuccess);
        Assert.Equal(UploadFormat.NpmLock, result.Value.Format);
        Assert.Equal(2, result.Value.Added);
        Assert.Equal(1, result.Value.Skipped); // geçersiz ad
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Contains(_audit.Events, e => e.EventType == AuditEventTypes.InventoryUploaded);
        Assert.Equal("editor", Assert.Single(_repository.Asset.InventoryUploads).UploadedBy);
        Assert.Equal(new[] { AssetId }, _scheduler.ScheduledAssets); // bulgular arka planda üretilir
    }

    [Fact]
    public async Task SameFile_CannotBeUploadedTwice()
    {
        var service = CreateService("editor", AssetGroupRole.Editor);
        await service.UploadAsync(Command(NpmLock));

        var second = await service.UploadAsync(Command(NpmLock));

        Assert.Equal(UploadErrors.AlreadyUploaded, second.Error);
    }

    [Fact]
    public async Task Viewer_CannotUpload_AndContentIsNotRead()
    {
        var service = CreateService("viewer", AssetGroupRole.Viewer);

        var result = await service.UploadAsync(new UploadInventoryCommand(AssetId, "package-lock.json", new ThrowingStream()));

        Assert.Equal(AssetGroupErrors.Forbidden, result.Error);
        Assert.Contains(_audit.Events, e => e.EventType == AuditEventTypes.AccessDenied);
        Assert.Equal(0, _unitOfWork.SaveCount);
        Assert.Empty(_scheduler.ScheduledAssets);
    }

    [Fact]
    public async Task NonMember_GetsNotFound_SoAssetExistenceIsNotLeaked()
    {
        var service = CreateService("stranger", role: null);

        var result = await service.UploadAsync(Command(NpmLock));

        Assert.Equal(AssetErrors.NotFound, result.Error);
        Assert.Contains(_audit.Events, e => e.EventType == AuditEventTypes.AccessDenied);
    }

    [Fact]
    public async Task OversizedFile_IsRejected_WithoutReadingEverything()
    {
        var service = CreateService("editor", AssetGroupRole.Editor);
        var huge = new FakeInfiniteStream();

        var result = await service.UploadAsync(new UploadInventoryCommand(AssetId, "big.json", huge));

        Assert.Equal("Upload.TooLarge", result.Error.Code);
        Assert.True(huge.BytesRead <= InventoryLimits.MaxUploadBytes + 81920);
    }

    [Fact]
    public async Task NonJsonExtension_IsRejected()
    {
        var service = CreateService("editor", AssetGroupRole.Editor);

        var result = await service.UploadAsync(new UploadInventoryCommand(AssetId, "evil.exe", new MemoryStream([1])));

        Assert.Equal(ZafiyetRadari.Domain.Common.ErrorType.Validation, result.Error.Type);
    }

    private InventoryUploadService CreateService(string userId, AssetGroupRole? role)
    {
        var currentUser = new FakeCurrentUser(userId);
        var guard = new InventoryAccessGuard(currentUser, new FakeAccessReader(role), _audit, NullLogger<InventoryAccessGuard>.Instance);

        return new InventoryUploadService(
            guard,
            _repository,
            new InventoryFileParser([new CycloneDxParser(), new NpmLockParser(), new NuGetLockParser()]),
            _unitOfWork,
            _audit,
            TimeProvider.System,
            new UploadInventoryCommandValidator(),
            _scheduler,
            NullLogger<InventoryUploadService>.Instance);
    }

    private static UploadInventoryCommand Command(string json) =>
        new(AssetId, @"C:\fakepath\package-lock.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private sealed class FakeCurrentUser(string userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public string? UserId => userId;
        public string? UserName => userId + "@test";
        public string? IpAddress => "127.0.0.1";
    }

    private sealed class FakeAccessReader(AssetGroupRole? role) : IAssetGroupAccessReader
    {
        public Task<AssetGroupRole?> GetRoleAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(assetGroupId == GroupId ? role : null);
    }

    private sealed class FakeAuditTrail : IAuditTrail
    {
        public List<AuditEvent> Events { get; } = [];

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AuditChainStatus> VerifyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditChainStatus(true, null, null, null));
    }

    private sealed class FakeScheduler : IMatchingScheduler
    {
        public List<int> ScheduledAssets { get; } = [];

        public void ScheduleAssetMatching(int assetId) => ScheduledAssets.Add(assetId);

        public void ScheduleFullMatching()
        {
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeRepository : IInventoryRepository
    {
        public Asset Asset { get; } = CreateAsset();

        public Task<int?> GetAssetGroupIdAsync(int assetId, CancellationToken cancellationToken) =>
            Task.FromResult(assetId == AssetId ? GroupId : (int?)null);

        public Task<Asset?> GetAssetWithComponentsAsync(int assetId, CancellationToken cancellationToken) =>
            Task.FromResult<Asset?>(assetId == AssetId ? Asset : null);

        public Task<bool> UploadExistsAsync(int assetId, byte[] sha256, CancellationToken cancellationToken) =>
            Task.FromResult(Asset.InventoryUploads.Any(u => u.Sha256.AsSpan().SequenceEqual(sha256)));

        public void AddGroup(AssetGroup group) => throw new NotSupportedException();
        public Task<AssetGroup?> GetGroupWithMembersAsync(int assetGroupId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteGroupAsync(int assetGroupId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void AddAsset(Asset asset) => throw new NotSupportedException();
        public Task<Asset?> GetAssetAsync(int assetId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> AssetNameExistsAsync(int assetGroupId, string name, int? excludingAssetId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAssetAsync(int assetId, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static Asset CreateAsset()
        {
            var asset = Asset.Create(GroupId, "web", AssetEnvironment.Production, AssetCriticality.High, true, DateTimeOffset.UtcNow).Value;
            asset.Id = AssetId;
            return asset;
        }
    }

    private sealed class ThrowingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Yetkisiz istekte içerik okunmamalı.");
    }

    private sealed class FakeInfiniteStream : Stream
    {
        public long BytesRead { get; private set; }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer.Span.Fill((byte)' ');
            BytesRead += buffer.Length;
            return ValueTask.FromResult(buffer.Length);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
