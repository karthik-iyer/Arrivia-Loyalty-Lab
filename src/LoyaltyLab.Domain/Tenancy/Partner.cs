using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tenancy;

public sealed class Partner : Entity<PartnerId>
{
    private Partner()
    {
    }

    private Partner(
        PartnerId id,
        string code,
        string displayName,
        Currency currency,
        PartnerTheme theme,
        CreditPolicy creditPolicy,
        QuotePolicy quotePolicy,
        SagaPolicy sagaPolicy,
        OpportunityPolicy opportunityPolicy)
        : base(id)
    {
        Code = code;
        DisplayName = displayName;
        Currency = currency;
        Theme = theme;
        CreditPolicy = creditPolicy;
        QuotePolicy = quotePolicy;
        SagaPolicy = sagaPolicy;
        OpportunityPolicy = opportunityPolicy;
    }

    public string Code { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public Currency Currency { get; private set; }

    public PartnerTheme Theme { get; private set; } = null!;

    public CreditPolicy CreditPolicy { get; private set; } = null!;

    public QuotePolicy QuotePolicy { get; private set; } = null!;

    public SagaPolicy SagaPolicy { get; private set; } = null!;

    public OpportunityPolicy OpportunityPolicy { get; private set; } = null!;

    public static Partner Create(
        string code,
        string displayName,
        Currency currency,
        PartnerTheme theme,
        CreditPolicy creditPolicy,
        QuotePolicy quotePolicy,
        SagaPolicy sagaPolicy,
        OpportunityPolicy opportunityPolicy,
        PartnerId? id = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(creditPolicy);
        ArgumentNullException.ThrowIfNull(quotePolicy);
        ArgumentNullException.ThrowIfNull(sagaPolicy);
        ArgumentNullException.ThrowIfNull(opportunityPolicy);

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Partner code is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Partner display name is required.");
        }

        return new Partner(
            id ?? PartnerId.New(),
            code.Trim().ToUpperInvariant(),
            displayName.Trim(),
            currency,
            theme,
            creditPolicy,
            quotePolicy,
            sagaPolicy,
            opportunityPolicy);
    }
}
