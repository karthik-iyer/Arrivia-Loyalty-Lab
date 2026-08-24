using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tests.Common;

public sealed class EntityTests
{
    [Fact]
    public void Entities_with_the_same_id_and_type_are_equal()
    {
        var id = PartnerId.New();
        var left = new SamplePartner(id);
        var right = new SamplePartner(id);

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        left.GetHashCode().Should().Be(right.GetHashCode());
    }

    [Fact]
    public void Entities_with_different_ids_are_not_equal()
    {
        new SamplePartner(PartnerId.New()).Should().NotBe(new SamplePartner(PartnerId.New()));
    }

    [Fact]
    public void Entities_of_different_types_are_not_equal_even_with_the_same_underlying_guid()
    {
        var guid = Guid.CreateVersion7();
        var partner = new SamplePartner(new PartnerId(guid));
        var member = new SampleMember(new MemberId(guid));

        Equals(partner, member).Should().BeFalse();
    }

    private sealed class SamplePartner(PartnerId id) : Entity<PartnerId>(id);

    private sealed class SampleMember(MemberId id) : Entity<MemberId>(id);
}
