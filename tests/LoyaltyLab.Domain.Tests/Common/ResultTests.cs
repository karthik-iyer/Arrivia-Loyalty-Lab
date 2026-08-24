using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Domain.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_the_value()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_throws_when_Error_is_read()
    {
        var result = Result<int>.Success(42);

        var act = () => result.Error;

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Failure_exposes_the_error()
    {
        var error = Error.Of("QUOTE_EXPIRED", "Quote has expired.");
        var result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Failure_throws_when_Value_is_read()
    {
        var result = Result<int>.Failure(Error.Of("QUOTE_EXPIRED", "Quote has expired."));

        var act = () => result.Value;

        act.Should().Throw<DomainException>().WithMessage("*QUOTE_EXPIRED*");
    }

    [Fact]
    public void Map_transforms_success_and_preserves_failure()
    {
        Result<int>.Success(21).Map(x => x * 2).Value.Should().Be(42);

        var failed = Result<int>.Failure(Error.Of("X", "x")).Map(x => x * 2);

        failed.IsFailure.Should().BeTrue();
        failed.Error.Code.Should().Be("X");
    }

    [Fact]
    public void Bind_does_not_invoke_the_binder_on_failure()
    {
        var invoked = false;
        var failed = Result<int>.Failure(Error.Of("X", "x")).Bind(_ =>
        {
            invoked = true;
            return Result<string>.Success("nope");
        });

        invoked.Should().BeFalse();
        failed.Error.Code.Should().Be("X");
    }

    [Fact]
    public void Bind_chains_a_successful_result()
    {
        var result = Result<int>.Success(21).Bind(x => Result<string>.Success(x.ToString(CultureInfo.InvariantCulture)));

        result.Value.Should().Be("21");
    }

    [Fact]
    public void Match_selects_the_success_or_failure_branch()
    {
        Result<int>.Success(1).Match(_ => "ok", _ => "fail").Should().Be("ok");
        Result<int>.Failure(Error.Of("X", "x")).Match(_ => "ok", e => e.Code).Should().Be("X");
    }
}
