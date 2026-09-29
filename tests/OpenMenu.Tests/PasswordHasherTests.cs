using OpenMenu.Infrastructure.Auth;
using Xunit;

namespace OpenMenu.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesIteratedSaltedFormat()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");

        var parts = hash.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations) && iterations >= 100_000);
        Assert.True(Convert.FromBase64String(parts[1]).Length >= 16);
        Assert.True(Convert.FromBase64String(parts[2]).Length >= 32);
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        var hash = PasswordHasher.Hash("s3cret!");
        Assert.True(PasswordHasher.Verify("s3cret!", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("s3cret!");
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Verify_RejectsMalformedStoredHash()
    {
        Assert.False(PasswordHasher.Verify("x", "not-a-valid-hash"));
    }

    [Fact]
    public void Hash_IsSaltedPerCall()
    {
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }
}
