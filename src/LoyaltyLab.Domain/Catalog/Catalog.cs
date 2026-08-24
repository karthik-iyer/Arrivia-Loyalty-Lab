using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Catalog;

public enum OfferTag
{
    Beach = 0,
    Ski = 1,
    City = 2,
    Family = 3,
    Luxury = 4,
}

public readonly record struct Destination
{
    public Destination(string code, string displayName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Destination code is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("Destination display name is required.");
        }

        Code = code.Trim().ToUpperInvariant();
        DisplayName = displayName.Trim();
    }

    public string Code { get; }

    public string DisplayName { get; }
}

public sealed class Supplier : Entity<SupplierId>
{
    private Supplier()
    {
    }

    private Supplier(SupplierId id, string code, string name)
        : base(id)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public static Supplier Create(string code, string name, SupplierId? id = null)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Supplier code and name are required.");
        }

        return new Supplier(id ?? SupplierId.New(), code.Trim().ToUpperInvariant(), name.Trim());
    }
}

public sealed class TravelOffer : Entity<OfferId>
{
    private TravelOffer()
    {
        Tags = [];
    }

    private TravelOffer(
        OfferId id,
        SupplierId supplierId,
        string propertyName,
        Destination destination,
        Money netRate,
        Money taxesAndFees,
        HashSet<OfferTag> tags,
        int starRating,
        DateOnly availableFrom,
        DateOnly availableTo)
        : base(id)
    {
        SupplierId = supplierId;
        PropertyName = propertyName;
        Destination = destination;
        NetRate = netRate;
        TaxesAndFees = taxesAndFees;
        Tags = tags;
        StarRating = starRating;
        AvailableFrom = availableFrom;
        AvailableTo = availableTo;
    }

    public SupplierId SupplierId { get; private set; }

    public string PropertyName { get; private set; } = null!;

    public Destination Destination { get; private set; }

    /// <summary>Never serialized to a member-facing DTO (FR-X-05).</summary>
    public Money NetRate { get; private set; }

    public Money TaxesAndFees { get; private set; }

    public HashSet<OfferTag> Tags { get; private set; } = [];

    public int StarRating { get; private set; }

    public DateOnly AvailableFrom { get; private set; }

    public DateOnly AvailableTo { get; private set; }

    public static TravelOffer Create(
        SupplierId supplierId,
        string propertyName,
        Destination destination,
        Money netRate,
        Money taxesAndFees,
        IEnumerable<OfferTag> tags,
        int starRating,
        DateOnly availableFrom,
        DateOnly availableTo,
        OfferId? id = null)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new DomainException("Property name is required.");
        }

        if (netRate.Currency != taxesAndFees.Currency)
        {
            throw new DomainException("Net rate and taxes must use the same currency.");
        }

        if (netRate.IsNegative || taxesAndFees.IsNegative)
        {
            throw new DomainException("Rates cannot be negative.");
        }

        if (starRating is < 1 or > 5)
        {
            throw new DomainException("Star rating must be between 1 and 5.");
        }

        if (availableTo < availableFrom)
        {
            throw new DomainException("Availability window ends before it starts.");
        }

        return new TravelOffer(
            id ?? OfferId.New(),
            supplierId,
            propertyName.Trim(),
            destination,
            netRate,
            taxesAndFees,
            tags.ToHashSet(),
            starRating,
            availableFrom,
            availableTo);
    }
}
