using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Pricing;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Pricing;

public sealed class ExplainQuote(
    ITenantContextAccessor tenant,
    IQuoteRepository quotes) : IUseCase<ExplainQuoteQuery, PriceExplanation>
{
    public async Task<Result<PriceExplanation>> ExecuteAsync(
        ExplainQuoteQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = tenant.Current;
        var quote = await quotes.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null)
        {
            return Result<PriceExplanation>.Failure(Errors.QuoteNotFound);
        }

        if (context.Role is AccessRole.Member or AccessRole.Anonymous
            && quote.MemberId != context.MemberId)
        {
            return Result<PriceExplanation>.Failure(Errors.QuoteNotFound);
        }

        return Result<PriceExplanation>.Success(PriceExplanation.FromQuote(quote, context.Role));
    }
}
