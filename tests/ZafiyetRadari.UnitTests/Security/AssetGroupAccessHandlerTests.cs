using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ZafiyetRadari.Application.Abstractions.Authorization;
using ZafiyetRadari.Domain.Enums;
using ZafiyetRadari.Web.Security;

namespace ZafiyetRadari.UnitTests.Security;

/// <summary>IDOR kontrolü: grup üyeliği ve rol seviyesi doğru uygulanıyor mu?</summary>
public sealed class AssetGroupAccessHandlerTests
{
    private const int GroupId = 42;

    [Theory]
    [InlineData(AssetGroupRole.Viewer, true, false, false)]
    [InlineData(AssetGroupRole.Editor, true, true, false)]
    [InlineData(AssetGroupRole.Owner, true, true, true)]
    public async Task Member_GetsAccordingToRole(AssetGroupRole role, bool canView, bool canEdit, bool canManage)
    {
        var handler = new AssetGroupAccessHandler(new FakeAccessReader(role));

        Assert.Equal(canView, await IsAuthorized(handler, AssetGroupOperations.View, User("user-1")));
        Assert.Equal(canEdit, await IsAuthorized(handler, AssetGroupOperations.Edit, User("user-1")));
        Assert.Equal(canManage, await IsAuthorized(handler, AssetGroupOperations.Manage, User("user-1")));
    }

    [Fact]
    public async Task NonMember_IsDenied_EvenIfAdmin()
    {
        var handler = new AssetGroupAccessHandler(new FakeAccessReader(role: null));
        var admin = User("admin-1", roles: ["Admin"]);

        Assert.False(await IsAuthorized(handler, AssetGroupOperations.View, admin));
    }

    [Fact]
    public async Task Anonymous_IsDenied()
    {
        var handler = new AssetGroupAccessHandler(new FakeAccessReader(AssetGroupRole.Owner));

        Assert.False(await IsAuthorized(handler, AssetGroupOperations.View, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private static async Task<bool> IsAuthorized(
        AssetGroupAccessHandler handler,
        AssetGroupAccessRequirement requirement,
        ClaimsPrincipal user)
    {
        var context = new AuthorizationHandlerContext([requirement], user, new AssetGroupResource(GroupId));
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal User(string userId, string[]? roles = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class FakeAccessReader(AssetGroupRole? role) : IAssetGroupAccessReader
    {
        public Task<AssetGroupRole?> GetRoleAsync(int assetGroupId, string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(assetGroupId == GroupId ? role : null);
    }
}
