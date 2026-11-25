using System.Data;
using Microsoft.Data.SqlClient;

namespace ZafiyetRadari.Migrator;

/// <summary>
/// Kayıtlı bir kullanıcıya Admin rolü verir. Uygulamada "ilk kayıt olan admin olur" gibi bir yol yoktur:
/// Admin sadece veritabanı yetkisine sahip operatör tarafından bu komutla verilir ve audit log'a yazılır.
/// </summary>
internal static class AdminRoleGranter
{
    private const string GrantSql = """
        DECLARE @UserId nvarchar(450) = (SELECT Id FROM auth.AspNetUsers WHERE NormalizedEmail = @NormalizedEmail);
        IF @UserId IS NULL THROW 50002, N'Bu e-posta ile kayıtlı kullanıcı yok. Önce uygulamadan kayıt ol.', 1;

        DECLARE @RoleId nvarchar(450) = (SELECT Id FROM auth.AspNetRoles WHERE NormalizedName = N'ADMIN');

        IF NOT EXISTS (SELECT 1 FROM auth.AspNetUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
        BEGIN
            INSERT INTO auth.AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);

            -- Security stamp değişince açık oturumlar yeniden doğrulanır; kullanıcı tekrar giriş yapıp yeni rolü alır.
            UPDATE auth.AspNetUsers SET SecurityStamp = CONVERT(nvarchar(36), NEWID()) WHERE Id = @UserId;

            EXEC ops.usp_AppendAuditLog
                @EventType  = 'Auth.RoleChanged',
                @UserId     = @UserId,
                @UserName   = @Email,
                @TargetType = 'Role',
                @TargetId   = N'Admin',
                @Details    = N'Migrator CLI ile Admin rolü verildi.';
        END
        """;

    public static async Task GrantAsync(string connectionString, string email, CancellationToken cancellationToken = default)
    {
        email = email.Trim();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = GrantSql;
        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 256) { Value = email });
        // Identity'nin varsayılan normalizer'ı ile aynı: ToUpperInvariant.
        command.Parameters.Add(new SqlParameter("@NormalizedEmail", SqlDbType.NVarChar, 256) { Value = email.ToUpperInvariant() });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
