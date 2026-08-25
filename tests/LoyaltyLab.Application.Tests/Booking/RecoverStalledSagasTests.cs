using LoyaltyLab.Application.Abstractions;
using LoyaltyLab.Application.Booking;
using LoyaltyLab.Domain.Booking;
using LoyaltyLab.Domain.Catalog;
using LoyaltyLab.Domain.Common;
using LoyaltyLab.Domain.Tenancy;

namespace LoyaltyLab.Application.Tests.Booking;

public sealed class RecoverStalledSagasTests
{
    [Fact]
    public async Task Stalled_saga_reaches_a_terminal_state()
    {
        var world = Harness.Create();
        await world.GrantAsync(5_000);
        world.Saga.MarkInProgress(SagaStepKind.ValidateQuote, world.Clock);
        world.Clock.UtcNow = world.Clock.UtcNow.AddSeconds(world.Context.Partner.SagaPolicy.StalledAfterSeconds);

        var recovered = await Recover(world).ExecuteAsync(CancellationToken.None);

        recovered.Should().Be(1);
        world.Saga.Status.Should().Be(SagaStatus.Confirmed);
        world.Saga.Steps.Should().OnlyContain(step => step.Status == SagaStepStatus.Succeeded);
    }

    [Fact]
    public async Task Fresh_heartbeat_is_left_alone()
    {
        var world = Harness.Create();
        world.Saga.MarkInProgress(SagaStepKind.ValidateQuote, world.Clock);

        var recovered = await Recover(world).ExecuteAsync(CancellationToken.None);

        recovered.Should().Be(0);
        world.Saga.Status.Should().Be(SagaStatus.Running);
        world.Saga.StepStatus(SagaStepKind.ValidateQuote).Should().Be(SagaStepStatus.InProgress);
    }

    private static RecoverStalledSagas Recover(Harness world)
    {
        var tenant = new FakeTenant { Current = TenantContext.ForMember(world.Context.Member) };
        var quotes = new FakeQuotes(tenant);
        quotes.AddAsync(world.Quote, CancellationToken.None).GetAwaiter().GetResult();
        var sagas = new FakeSagas();
        sagas.AddAsync(world.Saga, CancellationToken.None).GetAwaiter().GetResult();
        return new RecoverStalledSagas(
            sagas,
            new FakePartners(world.Context.Partner),
            quotes,
            new FakeMembers(world.Context.Member),
            new FakeOffers(world.Context.Offer),
            tenant,
            world.Orchestrator(),
            world.Clock);
    }
}
