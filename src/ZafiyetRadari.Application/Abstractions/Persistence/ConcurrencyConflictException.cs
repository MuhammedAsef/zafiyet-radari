namespace ZafiyetRadari.Application.Abstractions.Persistence;

/// <summary>
/// Kayıt okunduktan sonra başka bir işlem tarafından değiştirildi (rowversion) veya aynı anda aynı tekil kayıt eklendi.
/// Infrastructure, EF/SqlClient hatalarını bu tipe çevirir; Application katmanı EF'i bilmez.
/// </summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
