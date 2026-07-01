using Kotlet.Domain.Common;
using Xunit;

namespace Kotlet.Application.UnitTests.Common;

public sealed class EmailTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void TryCreate_RejectsInvalidInput(string? input) =>
        Assert.False(Email.TryCreate(input, out _));

    [Fact]
    public void TryCreate_TrimsWhitespace()
    {
        Assert.True(Email.TryCreate("  user@example.com  ", out var email));
        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void TryCreate_NormalizesCaseForLookups()
    {
        Assert.True(Email.TryCreate("User@Example.com", out var email));
        Assert.Equal("USER@EXAMPLE.COM", email.Normalized);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Email.TryCreate("user@example.com", out var first);
        Email.TryCreate("user@example.com", out var second);
        Assert.Equal(first, second);
    }
}
