using Dhathuru.Domain.Pricing;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Dhathuru.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Money_is_integer_minor_units()
    {
        Assert.Equal(15050, Money.FromMvr(150, 50).AmountMinor);
        Assert.Equal("150.50 MVR", Money.FromMvr(150, 50).ToString());
    }

    [Fact]
    public void Negative_amounts_are_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(-1));

    [Fact]
    public void Mixed_currencies_cannot_be_added() =>
        Assert.Throws<InvalidOperationException>(() => new Money(1, "USD") + new Money(1));

    [Fact]
    public void Subtraction_cannot_go_negative() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Money(5) - new Money(6));

    [Property]
    public Property Addition_is_commutative_and_exact(uint a, uint b) =>
        ((new Money(a) + new Money(b)) == (new Money(b) + new Money(a))
         && (new Money(a) + new Money(b)).AmountMinor == (long)a + b).ToProperty();

    [Property]
    public Property Scaling_equals_repeated_addition(uint amount, byte quantity) =>
        (new Money(amount) * quantity == Money.Sum(Enumerable.Repeat(new Money(amount), quantity))).ToProperty();
}
