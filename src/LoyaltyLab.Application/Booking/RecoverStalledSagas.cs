using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Booking;

/// <summary>
/// Resumes sagas whose heartbeat is older than the partner stall threshold (FR-B-11).
/// Derived idempotency keys make resumption safe after a crash.
/// </summary>
public sealed class RecoverStalledSagas(
    ISagaRepository sagas,
    IPartnerRepository partners,
    IQuoteRepository quotes,
    IMemberRepository members,
    IOfferRepository offers,
    ITenantContextAccessor tenant,
    AdvanceSaga advance,
    IClock clock)
{
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var active = await sagas.ListActiveAsync(cancellationToken);
        var recovered = 0;
        foreach (var saga in active)
        {
            tenant.Assign(TenantContext.Anonymous(saga.PartnerId));
            var partner = await partners.GetByIdAsync(saga.PartnerId, cancellationToken);
            if (partner is null || !saga.IsStalled(partner.SagaPolicy, clock))
            {
                continue;
            }

            var context = await LoadContextAsync(saga, partner, cancellationToken);
            if (context is null)
            {
                continue;
            }

            await advance.ExecuteAsync(context, cancellationToken);
            recovered++;
        }

        return recovered;
    }

    private async Task<SagaContext?> LoadContextAsync(
        SagaInstance saga,
        Partner partner,
        CancellationToken cancellationToken)
    {
        var quote = await quotes.GetByIdAsync(saga.Checkout.QuoteId, cancellationToken);
        if (quote is null)
        {
            return null;
        }

        var member = await members.GetByIdAsync(quote.MemberId, cancellationToken);
        var offer = await offers.GetByIdAsync(quote.OfferId, cancellationToken);
        if (member is null || offer is null)
        {
            return null;
        }

        return new SagaContext
        {
            Saga = saga,
            Quote = quote,
            Offer = offer,
            Partner = partner,
            Member = member,
            Tender = saga.Checkout.Tender,
            StayDate = saga.Checkout.StayDate,
            FloorAboveNet = saga.Checkout.FloorAboveNet,
        };
    }
}
