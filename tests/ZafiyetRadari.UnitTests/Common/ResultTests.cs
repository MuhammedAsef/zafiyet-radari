using ZafiyetRadari.Domain.Common;

namespace ZafiyetRadari.UnitTests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldCarryValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldCarryError_AndBlockValueAccess()
    {
        var error = Error.NotFound("Asset.NotFound", "Varlık bulunamadı.");
        Result<int> result = error;

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Failure_WithNoneError_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }
}
