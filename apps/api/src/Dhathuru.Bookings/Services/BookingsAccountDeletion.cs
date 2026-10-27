using Dhathuru.Bookings.Domain;
using Dhathuru.Domain.Seams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Anonymises a deleted user's bookings. The aggregates stay (ticket verification, refunds and accounting still need
/// them) but nothing that identifies the person does: names, phone numbers, contact email, saved-traveller links and
/// the owner id are replaced. Unpaid holds simply expire through the sweeper.
/// </summary>
internal sealed class BookingsAccountDeletion(BookingsDbContext db) : IAccountDeletionHandler
{
    public const string DeletedName = "Deleted traveller";

    public async Task OnAccountDeletingAsync(string userId, CancellationToken ct)
    {
        var ids = await db.Bookings.Where(b => b.UserId == userId).Select(b => b.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Set<BookingPassenger>().Where(p => ids.Contains(p.BookingId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.FullName, DeletedName)
                .SetProperty(p => p.Phone, (string?)null)
                .SetProperty(p => p.TravellerId, (string?)null), ct);
        // The owner id becomes unique per booking so the (UserId, CreateKey) index stays valid and nobody can read it back.
        foreach (var id in ids)
        {
            var anonymous = "deleted_" + id;
            await db.Bookings.Where(b => b.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.UserId, anonymous)
                    .SetProperty(b => b.ContactEmail, (string?)null), ct);
        }
        await tx.CommitAsync(ct);
    }
}

internal sealed class BookingsMigrator(IServiceScopeFactory scopes) : IModuleMigrator
{
    public string Name => "bookings";

    public async Task MigrateAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Database.MigrateAsync(ct);
    }
}
