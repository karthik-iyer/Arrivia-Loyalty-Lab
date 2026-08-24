using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyLab.Infrastructure.Persistence;

public sealed class QuoteRepository(LoyaltyLabDbContext db) : IQuoteRepository
{
    public Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken) =>
        db.Quotes.FirstOrDefaultAsync(quote => quote.Id == id, cancellationToken);

    public Task AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);
        db.Quotes.Add(quote);
        return Task.CompletedTask;
    }
}

public sealed class UnitOfWork(LoyaltyLabDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
