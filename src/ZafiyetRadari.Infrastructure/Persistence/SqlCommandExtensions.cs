using System.Data;
using Microsoft.Data.SqlClient;

namespace ZafiyetRadari.Infrastructure.Persistence;

/// <summary>
/// Ham ADO.NET sorgularında parametre eklemenin tek yolu.
/// Tip ve (metinsel tiplerde) boyut her zaman açıkça verilir. AddWithValue yasaktır (BannedSymbols.txt):
/// tip çıkarımı örtük dönüşümlere ve sorgu planı önbelleğinin şişmesine yol açar.
/// </summary>
internal static class SqlCommandExtensions
{
    public static SqlCommand WithParameter(this SqlCommand command, string name, SqlDbType type, object? value)
    {
        if (type is SqlDbType.NVarChar or SqlDbType.VarChar or SqlDbType.NChar or SqlDbType.Char or SqlDbType.VarBinary)
        {
            throw new ArgumentException($"'{type}' tipi için boyut belirtilmeli.", nameof(type));
        }

        return command.AddParameter(name, type, size: null, value);
    }

    public static SqlCommand WithParameter(this SqlCommand command, string name, SqlDbType type, int size, object? value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        return command.AddParameter(name, type, size, value);
    }

    private static SqlCommand AddParameter(this SqlCommand command, string name, SqlDbType type, int? size, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var parameter = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        if (size is not null)
        {
            parameter.Size = size.Value;
        }

        command.Parameters.Add(parameter);
        return command;
    }
}
