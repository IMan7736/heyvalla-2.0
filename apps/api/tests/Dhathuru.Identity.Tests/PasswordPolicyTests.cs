using System.Net;
using Dhathuru.Identity.Options;
using Dhathuru.Identity.Services;

namespace Dhathuru.Identity.Tests;

public class PasswordPolicyTests
{
    private static readonly AuthOptions Options = new();

    [Theory]
    [InlineData("correct horse battery staple")]
    [InlineData("0123456789 a b c")]
    [InlineData("ޑަންޑީ ބޯޓު ސަފަރު")] // Thaana is just text; length is what counts
    public void Long_enough_passwords_are_accepted_without_composition_rules(string password) =>
        Assert.Empty(PasswordPolicy.Check(password, "someone@example.com", Options));

    [Fact]
    public void Short_passwords_are_rejected() =>
        Assert.Equal(["PASSWORD_TOO_SHORT"], PasswordPolicy.Check("abc123", null, Options));

    [Fact]
    public void Passwords_longer_than_the_hasher_limit_are_rejected() =>
        Assert.Equal(["PASSWORD_TOO_LONG"], PasswordPolicy.Check(new string('a', 129), null, Options));

    [Theory]
    [InlineData("Password123")]
    [InlineData("qwertyuiop")]
    [InlineData("ababababababab")]
    public void Common_or_trivially_repetitive_passwords_are_rejected(string password) =>
        Assert.Contains("PASSWORD_TOO_COMMON", PasswordPolicy.Check(password, null, Options));

    [Fact]
    public void A_password_containing_the_account_name_is_rejected() =>
        Assert.Contains("PASSWORD_CONTAINS_EMAIL", PasswordPolicy.Check("my-aishath-passphrase", "aishath@example.com", Options));

    [Fact]
    public void Null_is_too_short() =>
        Assert.Equal(["PASSWORD_TOO_SHORT"], PasswordPolicy.Check(null, null, Options));
}

public class ClientInfoTests
{
    [Theory]
    [InlineData("203.0.113.57", "203.0.113.0/24")]
    [InlineData("::ffff:198.51.100.9", "198.51.100.0/24")]
    [InlineData("2001:db8:abcd:1234:5678:9abc:def0:1234", "2001:db8:abcd::/48")]
    public void Addresses_are_truncated_so_a_person_cannot_be_pinpointed(string address, string expected) =>
        Assert.Equal(expected, ClientInfo.Truncate(IPAddress.Parse(address)));

    [Fact]
    public void Missing_address_gives_no_hint() => Assert.Null(ClientInfo.Truncate(null));
}

public class SecretsTests
{
    [Fact]
    public void Tokens_are_long_unique_and_url_safe()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => Secrets.NewToken("rt_")).ToList();
        Assert.Equal(tokens.Count, tokens.Distinct().Count());
        Assert.All(tokens, t =>
        {
            Assert.StartsWith("rt_", t, StringComparison.Ordinal);
            Assert.True(t.Length >= 46);
            Assert.DoesNotContain('+', t);
            Assert.DoesNotContain('/', t);
            Assert.DoesNotContain('=', t);
        });
    }

    [Fact]
    public void Hashes_are_stable_hex_and_differ_per_token()
    {
        Assert.Equal(Secrets.Hash("a"), Secrets.Hash("a"));
        Assert.NotEqual(Secrets.Hash("a"), Secrets.Hash("b"));
        Assert.Equal(64, Secrets.Hash("a").Length);
    }
}

public class ConstantTimeTests
{
    [Fact]
    public async Task Fast_operations_are_padded_to_the_floor_and_failures_too()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await ConstantTime.RunAsync(150, () => Task.CompletedTask, CancellationToken.None);
        Assert.True(watch.ElapsedMilliseconds >= 140);

        watch.Restart();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ConstantTime.RunAsync(150, () => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        Assert.True(watch.ElapsedMilliseconds >= 140);
    }

    [Fact]
    public async Task A_zero_floor_adds_no_delay()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await ConstantTime.RunAsync(0, () => Task.CompletedTask, CancellationToken.None);
        Assert.True(watch.ElapsedMilliseconds < 100);
    }
}
