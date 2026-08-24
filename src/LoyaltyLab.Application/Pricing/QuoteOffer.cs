using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Pricing;

public sealed class QuoteOffer(
    ITenantContextAccessor tenant,
    IOfferRepository offers,
    IMemberRepository members,
    IPartnerRepository partners,
    IPricingRuleRepository rules,
    IPartnerSupplierRepository permits,
    IQuoteRepository quotes,
    IUnitOfWork unitOfWork,
    IClock clock) : IUseCase<QuoteOfferCommand, QuoteResult>
{
    public async Task<Result<QuoteResult>> ExecuteAsync(
        QuoteOfferCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = tenant.Current;
        if (!context.HasMember || context.MemberId is not { } memberId)
        {
            return Result<QuoteResult>.Failure(Errors.OfferNotFound);
        }

        var offer = await offers.GetByIdAsync(request.OfferId, cancellationToken);
        if (offer is null)
        {
            return Result<QuoteResult>.Failure(Errors.OfferNotFound);
        }

        var member = await members.GetByIdAsync(memberId, cancellationToken);
        var partner = await partners.GetByIdAsync(context.PartnerId, cancellationToken);
        if (member is null || partner is null)
        {
            return Result<QuoteResult>.Failure(Errors.OfferNotFound);
        }

        var asOf = clock.UtcNow;
        var permitted = await permits.GetPermittedSupplierIdsAsync(context.PartnerId, cancellationToken);
        var partnerRules = await rules.ListForPartnerAsync(context.PartnerId, asOf, cancellationToken);
        var priced = OfferPricing.Run(
            context.PartnerId,
            offer,
            context.Tier,
            request.StayDate,
            permitted,
            partnerRules,
            asOf);

        if (priced.IsRejected)
        {
            return Result<QuoteResult>.Failure(priced.RejectionReason ?? Errors.OfferNotEligible);
        }

        var quote = Quote.Create(member, offer, priced, partner.QuotePolicy, clock);
        await quotes.AddAsync(quote, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<QuoteResult>.Success(
            new QuoteResult(
                quote.Id,
                quote.OfferId,
                quote.MemberPrice,
                quote.MaxCreditTender,
                partner.CreditPolicy.ToCredits(quote.MaxCreditTender),
                quote.ExpiresAt));
    }
}
