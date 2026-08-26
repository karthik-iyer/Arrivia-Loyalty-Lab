using LoyaltyLab.Domain.Catalog;

namespace LoyaltyLab.Domain.Opportunity;

/// <summary>
/// A confirmed past stay used for destination and tag affinity (FR-O-04).
/// </summary>
public sealed record CompletedStay(Destination Destination, IReadOnlySet<OfferTag> Tags);
