namespace Dhathuru.Domain.Seams;

/// <summary>
/// Implemented by modules that hold personal data about a user (Bookings keeps anonymised aggregates).
/// Identity calls every registered handler when an account is deleted, before the account row is removed.
/// </summary>
public interface IAccountDeletionHandler
{
    Task OnAccountDeletingAsync(string userId, CancellationToken ct);
}
