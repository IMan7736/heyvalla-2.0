namespace Dhathuru.Domain.Pricing;

/// <summary>Integer minor units (MVR laari; 100 laari = 1 MVR). Never a floating point amount.</summary>
public readonly record struct Money
{
    public const string Mvr = "MVR";

    public Money(long amountMinor, string currency = Mvr)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountMinor);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        AmountMinor = amountMinor;
        Currency = currency;
    }

    public long AmountMinor { get; }

    public string Currency { get; } = Mvr;

    public static Money Zero => new(0);

    public static Money FromMvr(long wholeRufiyaa, int laari = 0) => new(checked(wholeRufiyaa * 100 + laari));

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(checked(a.AmountMinor + b.AmountMinor), a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(checked(a.AmountMinor - b.AmountMinor), a.Currency);
    }

    /// <summary>Scale by a whole number (for example passengers). Rounding never happens.</summary>
    public static Money operator *(Money a, int quantity) => new(checked(a.AmountMinor * quantity), a.Currency);

    public static Money Sum(IEnumerable<Money> values) => values.Aggregate(Zero, (a, b) => a + b);

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Currency mismatch: {a.Currency} vs {b.Currency}.");
    }

    public override string ToString() => $"{AmountMinor / 100}.{AmountMinor % 100:00} {Currency}";
}
