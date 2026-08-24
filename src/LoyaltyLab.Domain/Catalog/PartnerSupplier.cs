using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Domain.Catalog;

/// <summary>
/// A partner may sell a supplier's inventory only when this join exists.
/// Absence is a denial — NIMBUS has no row for OCEANIC.
/// </summary>
public sealed class PartnerSupplier : ITenantOwned
{
    private PartnerSupplier()
    {
    }

    private PartnerSupplier(PartnerId partnerId, SupplierId supplierId)
    {
        PartnerId = partnerId;
        SupplierId = supplierId;
    }

    public PartnerId PartnerId { get; private set; }

    public SupplierId SupplierId { get; private set; }

    public static PartnerSupplier Permit(PartnerId partnerId, SupplierId supplierId) =>
        new(partnerId, supplierId);
}
